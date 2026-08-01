using System.Collections.Generic;
using Ale.Chronicle;
using Ale.Toolkit.Runtime;
using Ale.Toolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Ale.Chronicle.Editor
{
    /// <summary>
    /// 角色检视器（组合装配 + 属性汇流实时预览）——Phase 1 核心交付。
    /// 身份自由字段（<see cref="AttributeFieldDrawer"/>）、特质芯片、核心属性区
    /// 实时调 <see cref="CoreAttributeResolver.Evaluate(CharacterDefinition, CoreAttributeDefinition, IChronicleSchemaSource)"/>
    /// 显示「基础 → 当前 + 逐来源明细」，家族指针。引用类字段用下拉。编辑走 RecordUndo → 改 → MarkDirty。
    /// </summary>
    public static class CharacterInspectorPanel
    {
        public static void Draw(IChronicleEditorContext ctx, CharacterDefinition c)
        {
            if (c == null)
            {
                EditorGUILayout.LabelField("请选择或新建一个角色。", ToolkitEditorStyles.Placeholder);
                return;
            }
            var db = ctx.Database;

            // ── 基础信息 ──────────────────────────────────────────────────────────
            EditorGUILayout.LabelField("基础信息", ToolkitEditorStyles.Header);
            EditorGUI.BeginChangeCheck();
            string id       = EditorGUILayout.TextField("ID", c.id);
            string template = NamePopup("角色模板", c.templateRef, Names(db.CharacterTemplates, t => t.name));
            if (EditorGUI.EndChangeCheck())
            {
                ctx.RecordUndo("修改角色");
                c.id          = id;
                c.templateRef = template;
                ctx.MarkDirty();   // 触发窗口 RefreshCaches → RebuildAttributes 同步自由字段
            }

            // ── 家族指针 ──────────────────────────────────────────────────────────
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("家族", ToolkitEditorStyles.Header);
            var otherIds = Names(db.Characters, x => x.id, exclude: c.id);
            EditorGUI.BeginChangeCheck();
            string father = NamePopup("父亲", c.fatherRef, otherIds);
            string mother = NamePopup("母亲", c.motherRef, otherIds);
            if (EditorGUI.EndChangeCheck())
            {
                ctx.RecordUndo("修改家族指针");
                c.fatherRef = father;
                c.motherRef = mother;
                ctx.MarkDirty();
            }
            EditorGUILayout.LabelField("子女 id", EditorStyles.miniBoldLabel);
            StringListEditor(ctx, "子女", c.childRefs);

            // ── 特质芯片 ──────────────────────────────────────────────────────────
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("特质", ToolkitEditorStyles.Header);
            DrawTraitChips(ctx, c, db);

            // ── 身份自由字段（schema = 模板 ∪ 特质功能标签）──────────────────────────
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("身份字段", ToolkitEditorStyles.Header);
            if (c.values.Count == 0)
                EditorGUILayout.LabelField("（无——请先为模板配置属性字段）", ToolkitEditorStyles.Placeholder);
            foreach (var entry in c.values)
                if (entry != null && entry.value != null)
                    AttributeFieldDrawer.Draw(ctx, entry.id, entry.value, null);

            // ── 核心属性：基础 → 当前 + 来源明细（实时预览）───────────────────────────
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("核心属性（基础 → 当前 + 来源明细）", ToolkitEditorStyles.Header);
            if (db.CoreAttributes.Count == 0)
            {
                EditorGUILayout.HelpBox("先在「属性」页签配置核心属性。", MessageType.None);
                return;
            }
            foreach (var def in db.CoreAttributes)
            {
                if (def == null || string.IsNullOrEmpty(def.id)) continue;

                float baseValue = c.GetCoreBaseValue(def.id, def);
                EditorGUI.BeginChangeCheck();
                float newBase = EditorGUILayout.FloatField(def.PlainName() + " 基础", baseValue);
                if (EditorGUI.EndChangeCheck())
                {
                    ctx.RecordUndo("修改角色基础属性");
                    SetBase(c, def.id, newBase);
                    ctx.MarkDirty();
                }

                // 实时汇流：从角色特质收集修饰器 → toolkit 求值 → clamp。
                var e = CoreAttributeResolver.Evaluate(c, def, db);
                EditorGUILayout.LabelField($"    基础 {e.BaseValue:0.##} → 当前 {e.Value:0.##}", EditorStyles.miniLabel);
                if (e.Breakdown != null)
                    foreach (var contrib in e.Breakdown)
                        EditorGUILayout.LabelField(
                            $"        {contrib.SourceTag}  {contrib.Operation} {contrib.Delta:+0.##;-0.##}",
                            EditorStyles.miniLabel);
                EditorGUILayout.Space(2);
            }
        }

        // ── 特质芯片 ───────────────────────────────────────────────────────────────

        private static void DrawTraitChips(IChronicleEditorContext ctx, CharacterDefinition c, ChronicleDatabase db)
        {
            int removeAt = -1;
            for (int i = 0; i < c.traits.Count; i++)
            {
                var ti = c.traits[i];
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorGUILayout.LabelField(string.IsNullOrEmpty(ti.traitRef) ? "(空)" : ti.traitRef, GUILayout.MinWidth(80));

                EditorGUI.BeginChangeCheck();
                float rem = EditorGUILayout.FloatField(ti.remainingDays, GUILayout.Width(60));
                if (EditorGUI.EndChangeCheck())
                {
                    ctx.RecordUndo("修改特质实例");
                    c.traits[i] = new CharacterTraitInstance(ti.traitRef, rem, ti.stacks, ti.sourceTag);
                    ctx.MarkDirty();
                }
                EditorGUILayout.LabelField(ti.remainingDays < 0f ? "永久" : "天", GUILayout.Width(30));

                if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(22))) removeAt = i;
                EditorGUILayout.EndHorizontal();
            }
            if (removeAt >= 0)
            {
                ctx.RecordUndo("移除特质");
                c.traits.RemoveAt(removeAt);
                ctx.MarkDirty();
            }

            // 添加特质下拉。
            var traitIds = Names(db.Traits, t => t.id);
            if (traitIds.Count > 0)
            {
                var options = new List<string> { "＋添加特质…" };
                options.AddRange(traitIds);
                int pick = EditorGUILayout.Popup(0, options.ToArray(), GUILayout.Width(160));
                if (pick > 0)
                {
                    string traitRef = options[pick];
                    var def = db.GetTrait(traitRef);
                    float rem = def != null && def.IsTemporary ? def.defaultDurationDays : -1f;
                    ctx.RecordUndo("添加特质");
                    c.traits.Add(new CharacterTraitInstance(traitRef, rem, 1, "editor"));
                    ctx.MarkDirty();
                }
            }
        }

        // ── 辅助 ──────────────────────────────────────────────────────────────────

        private static void SetBase(CharacterDefinition c, string attrId, float v)
        {
            for (int i = 0; i < c.coreAttributes.Count; i++)
                if (c.coreAttributes[i].attrId == attrId)
                {
                    c.coreAttributes[i] = new CoreAttributeValue(attrId, v);
                    return;
                }
            c.coreAttributes.Add(new CoreAttributeValue(attrId, v));
        }

        private static List<string> Names<T>(List<T> list, System.Func<T, string> keyOf, string exclude = null)
        {
            var result = new List<string>();
            if (list == null) return result;
            foreach (var e in list)
            {
                string k = e != null ? keyOf(e) : null;
                if (string.IsNullOrEmpty(k) || k == exclude || result.Contains(k)) continue;
                result.Add(k);
            }
            return result;
        }

        private static string NamePopup(string label, string current, List<string> options)
        {
            var display = new List<string>(options.Count + 2) { "(无)" };
            display.AddRange(options);
            int idx = 0;
            if (!string.IsNullOrEmpty(current))
            {
                int found = options.IndexOf(current);
                if (found >= 0) idx = found + 1;
                else { display.Add(current + " (悬空)"); idx = display.Count - 1; }
            }
            int newIdx = EditorGUILayout.Popup(label, idx, display.ToArray());
            if (newIdx <= 0) return "";
            if (newIdx <= options.Count) return options[newIdx - 1];
            return current;   // 悬空值保留
        }

        private static void StringListEditor(IChronicleEditorContext ctx, string noun, List<string> list)
        {
            if (list == null) return;
            int removeAt = -1;
            for (int i = 0; i < list.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                string v = EditorGUILayout.TextField(list[i]);
                if (EditorGUI.EndChangeCheck()) { ctx.RecordUndo("修改" + noun); list[i] = v; ctx.MarkDirty(); }
                if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(22))) removeAt = i;
                EditorGUILayout.EndHorizontal();
            }
            if (removeAt >= 0) { ctx.RecordUndo("删除" + noun); list.RemoveAt(removeAt); ctx.MarkDirty(); }
            if (GUILayout.Button("+ 添加" + noun, GUILayout.Width(90))) { ctx.RecordUndo("添加" + noun); list.Add(""); ctx.MarkDirty(); }
        }
    }
}
