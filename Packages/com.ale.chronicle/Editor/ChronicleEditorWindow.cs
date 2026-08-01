using System;
using System.Collections.Generic;
using System.IO;
using Ale.Chronicle.Serialization;
using Ale.Toolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Ale.Chronicle.Editor
{
    /// <summary>
    /// 编年史系统配置编辑器主窗口（IMGUI）。外壳（数据文件字段 / 页签条 / 导出按钮 / 状态栏 / Undo /
    /// 路径记忆 / 缓存刷新编排）全部来自 toolkit 的 <see cref="EditorDatabaseWindowBase{ChronicleDatabase}"/>；
    /// 本窗口只提供领域钩子：页签集合、创建数据库、缓存刷新（查重 + RebuildAttributes）、状态栏文案、
    /// 导出是否禁用、导出按钮。同时实现 <see cref="IChronicleEditorContext"/> 供各面板取用。
    /// </summary>
    public sealed class ChronicleEditorWindow : EditorDatabaseWindowBase<ChronicleDatabase>, IChronicleEditorContext
    {
        private readonly AttributeSystemTab _attributeTab = new AttributeSystemTab();
        private readonly TraitSystemTab     _traitTab     = new TraitSystemTab();
        private readonly CharacterSystemTab _characterTab = new CharacterSystemTab();
        private readonly SkillSystemTab     _skillTab     = new SkillSystemTab();
        private readonly GeneralSystemTab   _generalTab   = new GeneralSystemTab();
        private IEditorSystemTab<ChronicleDatabase>[] _tabs;

        // 各实体种类的重复 id/name 集合（Layout 阶段由 RefreshCaches 刷新）。
        private Dictionary<EChronicleEntityKind, HashSet<string>> _duplicateIds;

        /// <summary>窗口标题。</summary>
        private const string WindowTitle = "编年史编辑器";

        /// <summary>窗口初始尺寸（宽 × 高）：仅首次打开时应用，并居中于主编辑器窗口。</summary>
        private static readonly Vector2 WindowDefaultSize = new Vector2(1280f, 780f);

        [MenuItem("Tools/Ale Toolkit/Chronicle System/Chronicle Editor", priority = 1101)]
        public static void Open() => OpenWindow();

        /// <summary>打开窗口并载入指定数据库（供数据文件 Inspector 的「打开编辑器」按钮调用）。</summary>
        public static void Open(ChronicleDatabase db)
        {
            var window = OpenWindow();
            if (db) window.SetDatabase(db);
            window.Focus();
        }

        /// <summary>获取（或创建）窗口；首次创建时按 <see cref="WindowDefaultSize"/> 定尺寸并居中。</summary>
        private static ChronicleEditorWindow OpenWindow()
        {
            bool isNew = !HasOpenInstances<ChronicleEditorWindow>();
            var window = GetWindow<ChronicleEditorWindow>(WindowTitle);
            if (isNew)
            {
                var main = EditorGUIUtility.GetMainWindowPosition();
                float x = main.x + (main.width  - WindowDefaultSize.x) * 0.5f;
                float y = main.y + (main.height - WindowDefaultSize.y) * 0.5f;
                window.position = new Rect(x, y, WindowDefaultSize.x, WindowDefaultSize.y);
            }
            window.Show();
            return window;
        }

        // ── 基类钩子 ──────────────────────────────────────────────────────────────

        protected override string EditorPrefKey => "ChronicleSystem.DatabasePath";

        protected override string[] SystemTabLabels => new[] { "角色", "属性", "特质", "技能", "通用" };

        protected override IEditorSystemTab<ChronicleDatabase>[] SystemTabs
            => _tabs ??= new IEditorSystemTab<ChronicleDatabase>[] { _characterTab, _attributeTab, _traitTab, _skillTab, _generalTab };

        protected override string EmptyDatabaseHint => "请创建或选择一个 ChronicleDatabase 数据文件";

        protected override ChronicleDatabase CreateNewDatabase()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "创建编年史数据文件", "ChronicleDatabase", "asset", "请选择数据文件保存位置");
            if (string.IsNullOrEmpty(path)) return null;

            var db = CreateInstance<ChronicleDatabase>();
            AssetDatabase.CreateAsset(db, path);
            AssetDatabase.SaveAssets();
            return db;
        }

        protected override void RefreshCaches(ChronicleDatabase db)
        {
            _duplicateIds = ScanDuplicates(db);

            // 模板 / 特质标签 schema 变动后，同步所有角色的自由字段集合（幂等）。
            if (db)
            {
                foreach (var c in db.Characters)
                    c?.RebuildAttributes(db);
                // 技能模板 schema 变动后，同步所有技能的自定义字段集合（幂等）。
                foreach (var s in db.Skills)
                    s?.RebuildAttributes(db);
            }
        }

        protected override string BuildStatusMessage(ChronicleDatabase db)
        {
            if (_duplicateIds == null) return string.Empty;
            var parts = new List<string>();
            foreach (var kv in _duplicateIds)
                if (kv.Value.Count > 0)
                    parts.Add($"⚠ {NounOf(kv.Key)}重复：{string.Join(", ", kv.Value)}（导出已禁用）");
            return string.Join("  |  ", parts);
        }

        protected override bool IsExportBlocked(ChronicleDatabase db)
        {
            if (_duplicateIds == null) return false;
            foreach (var kv in _duplicateIds)
                if (kv.Value.Count > 0) return true;
            return false;
        }

        protected override void DrawExportButtons(ChronicleDatabase db)
        {
            if (GUILayout.Button("导出二进制", EditorStyles.toolbarButton, GUILayout.Width(90)))
                ExportBinary(db);
        }

        // ── IChronicleEditorContext ──────────────────────────────────────────────

        public HashSet<string> DuplicateIdsOf(EChronicleEntityKind kind)
        {
            _duplicateIds ??= ScanDuplicates(Database);
            return _duplicateIds.TryGetValue(kind, out var set) ? set : new HashSet<string>();
        }

        // ── 导出 ──────────────────────────────────────────────────────────────────

        private void ExportBinary(ChronicleDatabase db)
        {
            if (!db) return;
            if (!db.Validate(out var errors))
            {
                EditorUtility.DisplayDialog("无法导出", string.Join("\n", errors), "确定");
                return;
            }
            string path = EditorUtility.SaveFilePanel("导出为二进制", Application.dataPath, db.name, "bytes");
            if (string.IsNullOrEmpty(path)) return;

            File.WriteAllBytes(path, ChronicleConfigSerializer.Export(db, Resolver));
            AssetDatabase.Refresh();
            ShowNotification(new GUIContent("已导出二进制"));
        }

        // ── 查重 ──────────────────────────────────────────────────────────────────

        private static Dictionary<EChronicleEntityKind, HashSet<string>> ScanDuplicates(ChronicleDatabase db)
        {
            var result = new Dictionary<EChronicleEntityKind, HashSet<string>>();
            foreach (EChronicleEntityKind k in Enum.GetValues(typeof(EChronicleEntityKind)))
                result[k] = new HashSet<string>();
            if (!db) return result;

            Collect(db.CoreAttributes,     x => x?.id,   result[EChronicleEntityKind.CoreAttribute]);
            Collect(db.Traits,             x => x?.id,   result[EChronicleEntityKind.Trait]);
            Collect(db.CharacterTemplates, x => x?.name, result[EChronicleEntityKind.CharacterTemplate]);
            Collect(db.Characters,         x => x?.id,   result[EChronicleEntityKind.Character]);
            Collect(db.EnumTypesList,      x => x?.name, result[EChronicleEntityKind.EnumType]);
            Collect(db.Tags,               x => x?.name, result[EChronicleEntityKind.Tag]);
            Collect(db.Skills,             x => x?.id,   result[EChronicleEntityKind.Skill]);
            return result;
        }

        private static void Collect<T>(List<T> list, Func<T, string> keyOf, HashSet<string> dupOut)
        {
            if (list == null) return;
            var seen = new HashSet<string>();
            foreach (var e in list)
            {
                var key = keyOf(e);
                if (string.IsNullOrWhiteSpace(key)) continue;
                if (!seen.Add(key)) dupOut.Add(key);
            }
        }

        private static string NounOf(EChronicleEntityKind k) => k switch
        {
            EChronicleEntityKind.CoreAttribute     => "核心属性 id",
            EChronicleEntityKind.Trait             => "特质 id",
            EChronicleEntityKind.CharacterTemplate => "角色模板 name",
            EChronicleEntityKind.Character         => "角色 id",
            EChronicleEntityKind.EnumType          => "枚举类型 name",
            EChronicleEntityKind.Tag               => "功能标签 name",
            EChronicleEntityKind.Skill             => "技能 id",
            _                                      => k.ToString(),
        };
    }
}
