using System.Collections.Generic;
using Ale.Effect;
using Ale.Effect.Editor;
using Ale.GameplayTags;
using Ale.GameplayTags.Editor;
using Ale.Toolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Ale.Chronicle.Editor
{
    /// <summary>
    /// 效果系统相关的可复用 IMGUI 助手：效果引用列表（下拉选择 + 增删）、内联效果定义（交由 toolkit
    /// <c>EffectDefinitionDrawer</c> 渲染，隐藏被包装实体同步的 id / 显示名两行）。Undo / 标脏经 <see cref="IChronicleEditorContext"/>。
    /// </summary>
    public static class ChronicleEffectFields
    {
        /// <summary>当前正在内联绘制其效果定义的数据库（供 toolkit「属性 id」钩子取核心属性目录）；仅在绘制期间非空。</summary>
        public static ChronicleDatabase ActiveDatabase { get; private set; }

        private static ChronicleEffect _lastExpanded;

        // ── 效果引用列表 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 效果 id 引用列表：逐行下拉（显示名 (id)；悬空 / 未选择项保留在首位）+ <c>✕</c> 删除（延迟应用）+ 底部添加按钮
        /// （库中无效果时禁用并显示 <paramref name="emptyHint"/>）。调用方自绘上方标题。
        /// </summary>
        public static void EffectRefList(IChronicleEditorContext ctx, List<string> list, string undoNoun, string emptyHint)
        {
            if (list == null) return;
            var db = ctx.Database;

            var ids   = new List<string>();
            var names = new List<string>();
            if (db != null)
                foreach (var e in db.Effects)
                {
                    if (e == null || string.IsNullOrEmpty(e.id) || ids.Contains(e.id)) continue;
                    ids.Add(e.id);
                    string n = e.PlainName();
                    names.Add(n == e.id ? e.id : $"{n} ({e.id})");
                }

            int removeAt = -1;
            for (int i = 0; i < list.Count; i++)
            {
                string cur    = list[i] ?? string.Empty;
                var    optIds = new List<string>(ids);
                var    opts   = new List<string>(names);
                if (cur.Length == 0)               { optIds.Insert(0, string.Empty); opts.Insert(0, "（未选择）"); }
                else if (!optIds.Contains(cur))    { optIds.Insert(0, cur);          opts.Insert(0, cur + "（悬空）"); }

                EditorGUILayout.BeginHorizontal();
                int idx = Mathf.Max(0, optIds.IndexOf(cur));
                EditorGUI.BeginChangeCheck();
                int newIdx = EditorGUILayout.Popup(idx, opts.ToArray());
                if (EditorGUI.EndChangeCheck() && newIdx != idx)
                {
                    ctx.RecordUndo("修改" + undoNoun);
                    list[i] = optIds[newIdx];
                    ctx.MarkDirty();
                }
                if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(22f)))
                    removeAt = i;
                EditorGUILayout.EndHorizontal();
            }

            if (removeAt >= 0)
            {
                ctx.RecordUndo("删除" + undoNoun);
                list.RemoveAt(removeAt);
                ctx.MarkDirty();
            }

            using (new EditorGUI.DisabledScope(ids.Count == 0))
            {
                if (GUILayout.Button("+ 添加效果", GUILayout.Width(90f)))
                {
                    ctx.RecordUndo("添加" + undoNoun);
                    list.Add(ids[0]);
                    ctx.MarkDirty();
                }
            }
            if (ids.Count == 0 && !string.IsNullOrEmpty(emptyHint))
                EditorGUILayout.LabelField(emptyHint, EditorStyles.miniLabel);
        }

        // ── 内联效果定义 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 内联绘制 <paramref name="effect"/> 的 <see cref="ChronicleEffect.definition"/>：经数据库 <c>SerializedObject</c> 定位
        /// <c>effects[i].definition</c>，交由 toolkit <c>[CustomPropertyDrawer(EffectDefinition)]</c> 渲染；绘制期间隐藏定义内的
        /// id / 显示名两行（由包装实体同步），并把当前库暴露给「属性 id」下拉钩子。首次选中某效果时自动展开。
        /// </summary>
        public static void InlineDefinition(IChronicleEditorContext ctx, ChronicleEffect effect)
        {
            var so = ctx.Serialized;
            var db = ctx.Database;
            if (so == null || db == null || effect == null)
            {
                EditorGUILayout.HelpBox("效果定义编辑暂不可用（无序列化对象）。", MessageType.None);
                return;
            }

            so.Update();
            var arr = so.FindProperty("effects");
            int idx = db.Effects.IndexOf(effect);
            if (arr == null || idx < 0 || idx >= arr.arraySize)
            {
                EditorGUILayout.HelpBox("效果定义编辑暂不可用。", MessageType.None);
                return;
            }
            var prop = arr.GetArrayElementAtIndex(idx).FindPropertyRelative("definition");
            if (prop == null) return;

            if (!ReferenceEquals(_lastExpanded, effect))
            {
                _lastExpanded   = effect;
                prop.isExpanded = true;
            }

            bool prevShow = EffectDefinitionDrawerHooks.ShowIdentityFields;
            var  prevDb   = ActiveDatabase;
            EffectDefinitionDrawerHooks.ShowIdentityFields = false;
            ActiveDatabase = db;
            try
            {
                EditorGUILayout.PropertyField(prop, new GUIContent("效果定义"), true);
            }
            finally
            {
                EffectDefinitionDrawerHooks.ShowIdentityFields = prevShow;
                ActiveDatabase = prevDb;
            }
            so.ApplyModifiedProperties();
        }

        /// <summary>对「已同步 id / 显示名并归一」的定义副本做校验（不改动数据），返回错误与警告（警告已去掉前缀）。</summary>
        public static void ValidateForDisplay(ChronicleEffect effect, List<string> errors, List<string> warnings)
        {
            if (effect?.definition == null) { errors.Add("定义为空"); return; }
            var def = effect.definition.Clone();
            def.id          = effect.id;
            def.displayName = effect.PlainName();
            def.Normalize();
            var msgs = new List<string>();
            def.Validate(msgs);
            foreach (var m in msgs)
            {
                if (EffectDefinition.IsWarning(m)) warnings.Add(m.Substring(EffectDefinition.WarningPrefix.Length).Trim());
                else                               errors.Add(m);
            }
        }
    }

    /// <summary>
    /// 把数据库声明的 Gameplay 标签并入 toolkit 标签注册表并刷新编辑器标签目录（仅在标签集合变化时执行），
    /// 使效果的标签字段下拉与「未登记」状态标注能看到本库标签。注册表是咨询性的：运行时匹配不依赖它。
    /// </summary>
    public static class ChronicleGameplayTagSync
    {
        private static string _lastSignature;

        public static void Sync(ChronicleDatabase db)
        {
            if (!db) return;
            var sb = new System.Text.StringBuilder();
            foreach (var t in db.GameplayTags)
                if (t != null) sb.Append(t.name).Append('\n');
            string sig = sb.ToString();
            if (sig == _lastSignature) return;
            _lastSignature = sig;

            GameplayTagRuntime.Register(db.GameplayTags);
            GameplayTagEditorCatalog.Rebuild();
        }
    }

    /// <summary>
    /// 编辑器初始化时向 toolkit 效果定义绘制器注入「属性 id」下拉：候选取自 <see cref="ChronicleEffectFields.ActiveDatabase"/>
    /// 的核心属性（显示名 (id)）；未在编年史编辑器内绘制（或库无核心属性）时退化为文本框。悬空 / 未选择项保留在首位。
    /// </summary>
    [InitializeOnLoad]
    internal static class ChronicleEffectEditorHooks
    {
        static ChronicleEffectEditorHooks()
        {
            EffectDefinitionDrawerHooks.AttributeIdField = DrawAttributeId;
        }

        private static string DrawAttributeId(Rect rect, string current)
        {
            var db = ChronicleEffectFields.ActiveDatabase;
            current ??= string.Empty;

            var ids      = new List<string>();
            var displays = new List<string>();
            if (db != null)
                foreach (var a in db.CoreAttributes)
                {
                    if (a == null || string.IsNullOrEmpty(a.id) || ids.Contains(a.id)) continue;
                    ids.Add(a.id);
                    string n = a.displayName != null ? a.displayName.GetTextValue() : null;
                    displays.Add(string.IsNullOrEmpty(n) || n == a.id ? a.id : $"{n} ({a.id})");
                }

            if (ids.Count == 0)
                return EditorGUI.TextField(rect, current);

            if (current.Length == 0)            { ids.Insert(0, string.Empty); displays.Insert(0, "（未选择）"); }
            else if (!ids.Contains(current))    { ids.Insert(0, current);      displays.Insert(0, current + "（悬空）"); }

            int idx    = Mathf.Max(0, ids.IndexOf(current));
            int newIdx = EditorGUI.Popup(rect, idx, displays.ToArray());
            return ids[newIdx];
        }
    }
}
