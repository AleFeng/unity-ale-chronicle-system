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
    /// <summary>编年史编辑器的系统页签（与 <see cref="ChronicleEditorWindow.SystemTabLabels"/> 同序）。供外部跳转指定落点。</summary>
    public enum EChronicleTab
    {
        General    = 0,
        Character  = 1,
        Attribute  = 2,
        Trait      = 3,
        Profession = 4,
        Skill      = 5,
        Title      = 6,
    }

    public sealed class ChronicleEditorWindow : EditorDatabaseWindowBase<ChronicleDatabase>, IChronicleEditorContext
    {
        private readonly AttributeSystemTab _attributeTab = new AttributeSystemTab();
        private readonly TraitSystemTab     _traitTab     = new TraitSystemTab();
        private readonly CharacterSystemTab _characterTab = new CharacterSystemTab();
        private readonly SkillSystemTab     _skillTab     = new SkillSystemTab();
        private readonly ProfessionSystemTab _professionTab = new ProfessionSystemTab();
        private readonly TitleSystemTab      _titleTab      = new TitleSystemTab();
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

        /// <summary>打开窗口、载入数据库并切到指定页签。供外部跳转使用（如 Condition Editor 的「被引用」列表）。</summary>
        public static void Open(ChronicleDatabase db, EChronicleTab tab)
        {
            var window = OpenWindow();
            if (db) window.SetDatabase(db);
            window.SelectSystemTab((int)tab);
            window.Focus();
            window.Repaint();
        }

        /// <summary>打开到「特质」页并定位到指定特质（下一帧 Layout 激活右列 Inspector）。</summary>
        public static void OpenTrait(ChronicleDatabase db, string traitId)
            => OpenAt(db, EChronicleTab.Trait, w => w._traitTab.RequestSelect(Find(db?.Traits, t => t.id == traitId)));

        /// <summary>打开到「职业」页并定位到指定职业。</summary>
        public static void OpenProfession(ChronicleDatabase db, string professionId)
            => OpenAt(db, EChronicleTab.Profession, w => w._professionTab.RequestSelect(Find(db?.Professions, p => p.id == professionId)));

        /// <summary>打开到「头衔」页并定位到指定头衔。</summary>
        public static void OpenTitle(ChronicleDatabase db, string titleId)
            => OpenAt(db, EChronicleTab.Title, w => w._titleTab.RequestSelect(Find(db?.Titles, t => t.id == titleId)));

        /// <summary>打开到「属性」页并定位到指定核心属性。</summary>
        public static void OpenCoreAttribute(ChronicleDatabase db, string attrId)
            => OpenAt(db, EChronicleTab.Attribute, w => w._attributeTab.RequestSelect(Find(db?.CoreAttributes, a => a.id == attrId)));

        private static void OpenAt(ChronicleDatabase db, EChronicleTab tab, Action<ChronicleEditorWindow> select)
        {
            var window = OpenWindow();
            if (db) window.SetDatabase(db);
            window.SelectSystemTab((int)tab);
            select(window);                    // RequestSelect 须在数据库设定之后调用
            window.Focus();
            window.Repaint();
        }

        private static T Find<T>(List<T> list, Func<T, bool> match) where T : class
        {
            if (list == null) return null;
            foreach (var item in list)
                if (item != null && match(item)) return item;
            return null;
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

        // 效果 / Gameplay 标签自 0.5.0 起在 toolkit 的 Effect Editor（Tools > Ale Toolkit > Effect System）中配置，本窗口不再设「效果」页签。
        protected override string[] SystemTabLabels => new[] { "通用", "角色", "属性", "特质", "职业", "技能", "头衔" };

        protected override IEditorSystemTab<ChronicleDatabase>[] SystemTabs
            => _tabs ??= new IEditorSystemTab<ChronicleDatabase>[] { _generalTab, _characterTab, _attributeTab, _traitTab, _professionTab, _skillTab, _titleTab };

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
                // 职业 / 转职树：反序列化 / 编辑后归一（幂等）；职业模板 schema 变动后同步自定义字段。
                foreach (var p in db.Professions)     { p?.Normalize(); p?.RebuildAttributes(db); }
                foreach (var t in db.ProfessionTrees) t?.Normalize();
                foreach (var t in db.Titles)          { t?.Normalize(); t?.RebuildAttributes(db); }
                foreach (var l in db.RankLadders)     l?.Normalize();
                foreach (var st in db.SkillTrees)     st?.Normalize();
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
            Collect(db.Professions,        x => x?.id,   result[EChronicleEntityKind.Profession]);
            Collect(db.ProfessionTrees,    x => x?.id,   result[EChronicleEntityKind.ProfessionTree]);
            Collect(db.Titles,             x => x?.id,   result[EChronicleEntityKind.Title]);
            Collect(db.RankLadders,        x => x?.id,   result[EChronicleEntityKind.RankLadder]);
            Collect(db.SkillTrees,         x => x?.id,   result[EChronicleEntityKind.SkillTree]);
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
            EChronicleEntityKind.Profession        => "职业 id",
            EChronicleEntityKind.ProfessionTree    => "转职树 id",
            EChronicleEntityKind.Title             => "头衔 id",
            EChronicleEntityKind.RankLadder        => "阶级序列 id",
            EChronicleEntityKind.SkillTree         => "技能树 id",
            _                                      => k.ToString(),
        };
    }
}
