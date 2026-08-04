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
            ChronicleEntityHeader.DrawIdField(ctx, "角色", c.id,
                ctx.DuplicateIdsOf(EChronicleEntityKind.Character), v => c.id = v);
            ChronicleEntityHeader.DrawTemplateRefReadonly(c.templateRef);   // 来源模板创建后只读（经中列「从模板添加」设定）

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
            ChronicleEditorFields.StringList(ctx, c.childRefs, "子女", "+ 添加子女", 90f);

            // ── 特质芯片 ──────────────────────────────────────────────────────────
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("特质", ToolkitEditorStyles.Header);
            DrawTraitChips(ctx, c, db);

            // ── 职业（等级 / 经验 / 主职业 + 该级成长预览）────────────────────────────
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("职业", ToolkitEditorStyles.Header);
            DrawProfessionSection(ctx, c, db);

            // ── 头衔（按 kind 标注阶级 / 称号）────────────────────────────────────────
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("头衔", ToolkitEditorStyles.Header);
            DrawTitleSection(ctx, c, db);

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

                // 实时汇流：从角色特质 / 职业成长 / 头衔收集修饰器 → toolkit 求值 → clamp。
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

        // ── 职业 ───────────────────────────────────────────────────────────────────

        private static void DrawProfessionSection(IChronicleEditorContext ctx, CharacterDefinition c, ChronicleDatabase db)
        {
            var profIds = Names(db.Professions, p => p.id);
            int removeAt = -1;
            for (int i = 0; i < c.professions.Count; i++)
            {
                var cp = c.professions[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                string profRef = NamePopup("职业", cp.professionRef, profIds);
                bool changedRef = EditorGUI.EndChangeCheck();
                if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(22))) removeAt = i;
                EditorGUILayout.EndHorizontal();

                EditorGUI.BeginChangeCheck();
                int  level   = EditorGUILayout.IntField("等级", cp.level);
                int  exp     = EditorGUILayout.IntField("当前经验", cp.currentExp);
                bool primary = EditorGUILayout.Toggle("主职业", cp.isPrimary);
                bool changedFields = EditorGUI.EndChangeCheck();

                if (changedRef || changedFields)
                {
                    ctx.RecordUndo("修改角色职业");
                    c.professions[i] = new CharacterProfession(profRef, Mathf.Max(1, level), Mathf.Max(0, exp), primary);
                    if (primary && !cp.isPrimary) ClearOtherPrimary(c, i);
                    ctx.MarkDirty();
                }

                // 该级成长预览（与运行时同一 CollectGrowthModifiers）。
                var prof = db.GetProfession(cp.professionRef);
                if (prof != null)
                {
                    var mods = new List<ModifierDefinition>();
                    prof.CollectGrowthModifiers(cp.level, null, mods);
                    foreach (var m in mods)
                        EditorGUILayout.LabelField($"    {m.targetAttributeId}  +{m.magnitude:0.##}", EditorStyles.miniLabel);
                }
                EditorGUILayout.EndVertical();
            }
            if (removeAt >= 0) { ctx.RecordUndo("移除职业"); c.professions.RemoveAt(removeAt); ctx.MarkDirty(); }

            if (profIds.Count > 0)
            {
                var options = new List<string> { "＋添加职业…" };
                options.AddRange(profIds);
                int pick = EditorGUILayout.Popup(0, options.ToArray(), GUILayout.Width(160));
                if (pick > 0)
                {
                    ctx.RecordUndo("添加职业");
                    c.professions.Add(new CharacterProfession(options[pick], 1, 0, c.professions.Count == 0));
                    ctx.MarkDirty();
                }
            }
        }

        private static void ClearOtherPrimary(CharacterDefinition c, int keepIndex)
        {
            for (int j = 0; j < c.professions.Count; j++)
                if (j != keepIndex && c.professions[j].isPrimary)
                {
                    var p = c.professions[j];
                    c.professions[j] = new CharacterProfession(p.professionRef, p.level, p.currentExp, false);
                }
        }

        // ── 头衔 ───────────────────────────────────────────────────────────────────

        private static void DrawTitleSection(IChronicleEditorContext ctx, CharacterDefinition c, ChronicleDatabase db)
        {
            int removeAt = -1;
            for (int i = 0; i < c.titles.Count; i++)
            {
                var ct  = c.titles[i];
                var def = db.GetTitle(ct.titleRef);
                string kindTag = def != null ? (def.kind == ETitleKind.RankTitle ? "[阶级] " : "[称号] ") : "";

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorGUILayout.LabelField(kindTag + (string.IsNullOrEmpty(ct.titleRef) ? "(空)" : ct.titleRef), GUILayout.MinWidth(120));
                EditorGUI.BeginChangeCheck();
                int day = EditorGUILayout.IntField("获得日", ct.acquiredWorldDay, GUILayout.Width(150));
                if (EditorGUI.EndChangeCheck())
                {
                    ctx.RecordUndo("修改头衔");
                    c.titles[i] = new CharacterTitle(ct.titleRef, day);
                    ctx.MarkDirty();
                }
                if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(22))) removeAt = i;
                EditorGUILayout.EndHorizontal();
            }
            if (removeAt >= 0) { ctx.RecordUndo("移除头衔"); c.titles.RemoveAt(removeAt); ctx.MarkDirty(); }

            var titleIds = Names(db.Titles, t => t.id);
            if (titleIds.Count > 0)
            {
                var options = new List<string> { "＋添加头衔…" };
                options.AddRange(titleIds);
                int pick = EditorGUILayout.Popup(0, options.ToArray(), GUILayout.Width(160));
                if (pick > 0)
                {
                    ctx.RecordUndo("添加头衔");
                    c.titles.Add(new CharacterTitle(options[pick], 0));
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
    }
}
