using System.Collections.Generic;
using Ale.Chronicle;
using Ale.Toolkit.Runtime;
using Ale.Toolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Ale.Chronicle.Editor
{
    /// <summary>
    /// 「特质」页签（三列）：左=特质模板列表、中=特质列表（模板过滤 / 搜索 / 从模板添加 / 快速添加）、
    /// 右=特质 Inspector（选中特质时）或特质模板 Inspector（选中左列模板时）。
    /// </summary>
    public sealed class TraitSystemTab : EditorThreeColumnTab<TraitDefinition>
    {
        private readonly TraitTemplatePanel _templatePanel = new TraitTemplatePanel();
        private readonly TraitListPanel     _listPanel     = new TraitListPanel();
        private IEditorMasterListPanel<ChronicleDatabase>[] _leftPanels;

        protected override IEditorMasterListPanel<ChronicleDatabase>[] LeftPanels
            => _leftPanels ??= new IEditorMasterListPanel<ChronicleDatabase>[] { _templatePanel };

        protected override string EntityNoun => "特质";

        protected override List<TraitDefinition> EntityList(ChronicleDatabase db) => db.Traits;

        protected override TraitDefinition DrawEntityList(IChronicleEditorContext ctx, TraitDefinition displaySelected)
            => _listPanel.DrawList(ctx, displaySelected);

        protected override TraitDefinition ConsumePendingSelect() => _listPanel.ConsumePendingSelect();

        protected override void DrawEntityInspector(IChronicleEditorContext ctx, TraitDefinition entity)
            => TraitInspectorPanel.Draw(ctx, entity);
    }

    /// <summary>特质列表面板（中列）：模板过滤 + 搜索 + 从模板添加 / 快速添加，每行 id / 名称 / 时效·修饰器数。</summary>
    public sealed class TraitListPanel : EditorEntityListPanel<TraitDefinition, TraitTemplate>
    {
        public TraitListPanel() : base("ChronicleTraitListDrag") { }

        protected override EChronicleEntityKind Kind => EChronicleEntityKind.Trait;
        protected override string Noun => "特质";

        protected override List<TraitDefinition> Entities(ChronicleDatabase db) => db.Traits;
        protected override List<TraitTemplate>   Templates(ChronicleDatabase db) => db.TraitTemplates;
        protected override string TemplateName(TraitTemplate t) => t.name;
        protected override string TemplateRefOf(TraitDefinition e) => e.templateRef;
        protected override string IdOf(TraitDefinition e) => e.id;

        protected override Color RowDotColor(ChronicleDatabase db, TraitDefinition e)
        {
            var t = db.GetTraitTemplate(e.templateRef);
            return t != null ? t.color : Color.gray;
        }

        protected override bool Matches(ChronicleDatabase db, TraitDefinition e, string term)
        {
            if (string.IsNullOrEmpty(term)) return true;
            term = term.ToLowerInvariant();
            if (!string.IsNullOrEmpty(e.id) && e.id.ToLowerInvariant().Contains(term)) return true;
            string name = e.PlainName();
            return !string.IsNullOrEmpty(name) && name.ToLowerInvariant().Contains(term);
        }

        protected override TraitDefinition AddFromTemplate(IChronicleEditorContext ctx, string templateName)
        {
            var db = ctx.Database;
            ctx.RecordUndo("从模板添加特质");
            var t = db.GetTraitTemplate(templateName);
            var tr = new TraitDefinition(GenerateId(db, "trait_", id => db.GetTrait(id) != null))
            {
                templateRef = templateName,
            };
            if (t != null)
            {
                tr.categoryEnumRef     = t.categoryEnumRef;
                tr.lifetime            = t.lifetime;
                tr.defaultDurationDays = t.defaultDurationDays;
            }
            tr.displayName.SetTextValue(0, "新特质");
            tr.RebuildAttributes(db);
            db.Traits.Add(tr);
            ctx.MarkDirty();
            return tr;
        }

        protected override TraitDefinition QuickAdd(IChronicleEditorContext ctx)
        {
            var db = ctx.Database;
            if (db.Traits.Count == 0)
                return AddFromTemplate(ctx, db.TraitTemplates.Count > 0 ? db.TraitTemplates[0].name : null);

            ctx.RecordUndo("快速添加特质");
            var clone = db.Traits[db.Traits.Count - 1].Clone();
            clone.id = GenerateId(db, "trait_", id => db.GetTrait(id) != null);
            db.Traits.Add(clone);
            ctx.MarkDirty();
            return clone;
        }

        protected override void DrawRowColumns(ChronicleDatabase db, TraitDefinition e,
            Rect keyRow, float contentX, float contentRight, float valY, float valH)
        {
            float w     = Mathf.Max(0f, contentRight - contentX);
            float idW   = Mathf.Min(110f, w * 0.32f);
            float infoW = Mathf.Min(120f, w * 0.34f);
            float nameX = contentX + idW + Pad;
            float infoX = contentRight - infoW;
            float nameW = Mathf.Max(0f, infoX - Pad - nameX);

            GUI.Label(new Rect(contentX, keyRow.y, idW, keyRow.height), "ID", KeyStyle);
            GUI.Label(new Rect(nameX, keyRow.y, nameW, keyRow.height), "名称", KeyStyle);
            GUI.Label(new Rect(infoX, keyRow.y, infoW, keyRow.height), "时效·修饰器", KeyStyle);

            GUI.Label(new Rect(contentX, valY, idW, valH), string.IsNullOrEmpty(e.id) ? "(空 ID)" : e.id, IdStyle);
            GUI.Label(new Rect(nameX, valY, nameW, valH), e.PlainName(), SubStyle);
            int modCount = e.modifiers != null ? e.modifiers.Count : 0;
            GUI.Label(new Rect(infoX, valY, infoW, valH), $"{(e.IsTemporary ? "临时" : "永久")}·{modCount}", SubStyle);
        }
    }

    /// <summary>特质模板主列表面板（特质页左列）：绑定 <see cref="ChronicleDatabase.TraitTemplates"/> + 模板检视器。</summary>
    public sealed class TraitTemplatePanel : EditorMasterListPanel<TraitTemplate>
    {
        private readonly AttributeDefinitionListDrawer _schemaDrawer = new AttributeDefinitionListDrawer();

        protected override List<TraitTemplate> GetList(ChronicleDatabase db) => db.TraitTemplates;
        protected override string Noun => "特质模板";
        protected override bool   HasColorDot => true;
        protected override Color  RowColor(TraitTemplate item) => item.color;

        protected override string RowLabel(TraitTemplate item)
            => string.IsNullOrEmpty(item.name) ? "(未命名)" : item.name;

        protected override TraitTemplate CreateNew(ChronicleDatabase db, List<TraitTemplate> list)
        {
            int n = list.Count + 1;
            string name;
            do { name = "trait_template_" + n; n++; } while (Contains(list, name));
            return new TraitTemplate(name);
        }

        private static bool Contains(List<TraitTemplate> list, string name)
        {
            foreach (var t in list) if (t != null && t.name == name) return true;
            return false;
        }

        protected override void OnInvalidate() => _schemaDrawer.Invalidate();

        public override void DrawInspector(IChronicleEditorContext ctx, TraitTemplate tmpl)
        {
            if (tmpl == null)
            {
                EditorGUILayout.LabelField("请选择或新建一个特质模板。", ToolkitEditorStyles.Placeholder);
                return;
            }

            EditorGUILayout.LabelField("基础信息", ToolkitEditorStyles.Header);
            EditorGUI.BeginChangeCheck();
            string name     = EditorGUILayout.TextField("名称(引用键)", tmpl.name);
            Color  color    = EditorGUILayout.ColorField("列表色点", tmpl.color);
            string category = EditorGUILayout.TextField("默认类别枚举(可空)", tmpl.categoryEnumRef);
            var    lifetime = (ETraitLifetime)EditorGUILayout.EnumPopup("默认时效", tmpl.lifetime);
            float  durDays  = tmpl.defaultDurationDays;
            if (lifetime == ETraitLifetime.Temporary)
                durDays = EditorGUILayout.FloatField("默认存活天数", tmpl.defaultDurationDays);
            if (EditorGUI.EndChangeCheck())
            {
                ctx.RecordUndo("修改特质模板");
                tmpl.name                = name;
                tmpl.color               = color;
                tmpl.categoryEnumRef     = category;
                tmpl.lifetime            = lifetime;
                tmpl.defaultDurationDays = durDays;
                ctx.MarkDirty();
            }

            EditorGUILayout.Space(4);
            _schemaDrawer.Draw(ctx, ctx.Database, tmpl.attributes, "自定义字段 schema");
        }
    }

    /// <summary>特质实例检视器（特质页右列）：基础信息 / 显示·资源 / 遗传·出生 / 修饰器 / 互斥·相性·AI权重 / 自定义字段 / 获得条件。</summary>
    public static class TraitInspectorPanel
    {
        public static void Draw(IChronicleEditorContext ctx, TraitDefinition trait)
        {
            if (trait == null)
            {
                EditorGUILayout.LabelField("请选择或新建一个特质。", ToolkitEditorStyles.Placeholder);
                return;
            }
            trait.Normalize();
            var db = ctx.Database;

            // ── 基础信息 ──────────────────────────────────────────────────────────
            EditorGUILayout.LabelField("基础信息", ToolkitEditorStyles.Header);
            ChronicleEntityHeader.DrawIdField(ctx, "特质", trait.id,
                ctx.DuplicateIdsOf(EChronicleEntityKind.Trait), v => trait.id = v);
            ChronicleEntityHeader.DrawTemplateRefReadonly(trait.templateRef);

            EditorGUI.BeginChangeCheck();
            var    lifetime = (ETraitLifetime)EditorGUILayout.EnumPopup("时效", trait.lifetime);
            float  durDays  = trait.defaultDurationDays;
            bool   refresh  = trait.durationStacksRefresh;
            if (lifetime == ETraitLifetime.Temporary)
            {
                durDays = EditorGUILayout.FloatField("默认存活天数", trait.defaultDurationDays);
                refresh = EditorGUILayout.Toggle("重复获得刷新时长", trait.durationStacksRefresh);
            }
            string category = EditorGUILayout.TextField("类别枚举(可空)", trait.categoryEnumRef);
            string group    = EditorGUILayout.TextField("互斥组(可空)", trait.groupEquivalenceRef);
            string tagRef   = EditorGUILayout.TextField("功能标签(可空)", trait.functionTagRef);
            if (EditorGUI.EndChangeCheck())
            {
                ctx.RecordUndo("修改特质");
                trait.lifetime              = lifetime;
                trait.defaultDurationDays   = durDays;
                trait.durationStacksRefresh = refresh;
                trait.categoryEnumRef       = category;
                trait.groupEquivalenceRef   = group;
                trait.functionTagRef        = tagRef;
                ctx.MarkDirty();
            }

            // ── 显示 / 资源 ───────────────────────────────────────────────────────
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("显示 / 资源", ToolkitEditorStyles.Header);
            AttributeFieldDrawer.Draw(ctx, "显示名", trait.displayName, null);
            AttributeFieldDrawer.Draw(ctx, "说明",   trait.description, null);
            AttributeFieldDrawer.Draw(ctx, "图标",   trait.icon, null);

            // ── 遗传 / 出生 ───────────────────────────────────────────────────────
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("遗传 / 出生", ToolkitEditorStyles.Header);
            EditorGUI.BeginChangeCheck();
            bool  genetic = EditorGUILayout.Toggle("可遗传", trait.genetic);
            float inherit = EditorGUILayout.Slider("遗传概率", trait.inheritChance, 0f, 1f);
            float birth   = EditorGUILayout.Slider("先天概率", trait.birthChance, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                ctx.RecordUndo("修改特质遗传");
                trait.genetic       = genetic;
                trait.inheritChance = inherit;
                trait.birthChance   = birth;
                ctx.MarkDirty();
            }

            // ── 修饰器 ────────────────────────────────────────────────────────────
            EditorGUILayout.Space(4);
            ModifierListDrawer.Draw(ctx, "修饰器", trait.modifiers, db);

            // ── 互斥 / 相性 / AI 权重 ─────────────────────────────────────────────
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("互斥特质", ToolkitEditorStyles.Header);
            StringListEditor(ctx, "互斥特质 id", trait.incompatibleTraitRefs);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("相性", ToolkitEditorStyles.Header);
            CompatibilityListEditor(ctx, trait.compatibilities);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("AI 权重", ToolkitEditorStyles.Header);
            AiWeightListEditor(ctx, trait.aiWeights);

            // ── 自定义字段（来自模板 schema）───────────────────────────────────────
            EditorGUILayout.Space(4);
            var tmpl = db.GetTraitTemplate(trait.templateRef);
            ChronicleEntityHeader.DrawCustomAttributes(ctx, trait.values, tmpl?.attributes, "（无——模板未定义自定义字段）");

            // ── 获得条件（Condition System 内联绘制器）──────────────────────────────
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("获得条件 (eligibility)", ToolkitEditorStyles.Header);
            DrawEligibility(ctx, trait);
        }

        /// <summary>
        /// 通过数据库 SerializedObject 取到该特质的 <c>eligibility</c> 属性，交由 Ale.Condition 的
        /// 内联 <c>[CustomPropertyDrawer(typeof(ConditionExpression))]</c> 渲染（声明字段即配即用）。
        /// </summary>
        private static void DrawEligibility(IChronicleEditorContext ctx, TraitDefinition trait)
        {
            var so = ctx.Serialized;
            if (so == null || ctx.Database == null)
            {
                EditorGUILayout.HelpBox("条件编辑暂不可用（无序列化对象）。", MessageType.None);
                return;
            }

            so.Update();
            var traitsProp = so.FindProperty("traits");
            int idx = ctx.Database.Traits.IndexOf(trait);
            if (traitsProp == null || idx < 0 || idx >= traitsProp.arraySize)
            {
                EditorGUILayout.HelpBox("条件编辑暂不可用。", MessageType.None);
                return;
            }

            var eligProp = traitsProp.GetArrayElementAtIndex(idx).FindPropertyRelative("eligibility");
            if (eligProp == null) return;

            EditorGUILayout.PropertyField(eligProp, new GUIContent("条件"), true);
            so.ApplyModifiedProperties();
        }

        // ── 列表小编辑器 ───────────────────────────────────────────────────────────

        private static void StringListEditor(IChronicleEditorContext ctx, string label, List<string> list)
        {
            if (list == null) return;
            int removeAt = -1;
            for (int i = 0; i < list.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                string v = EditorGUILayout.TextField(list[i]);
                if (EditorGUI.EndChangeCheck()) { ctx.RecordUndo("修改" + label); list[i] = v; ctx.MarkDirty(); }
                if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(22))) removeAt = i;
                EditorGUILayout.EndHorizontal();
            }
            if (removeAt >= 0) { ctx.RecordUndo("删除" + label); list.RemoveAt(removeAt); ctx.MarkDirty(); }
            if (GUILayout.Button("+ 添加", GUILayout.Width(80))) { ctx.RecordUndo("添加" + label); list.Add(""); ctx.MarkDirty(); }
        }

        private static void CompatibilityListEditor(IChronicleEditorContext ctx, List<TraitCompatibility> list)
        {
            if (list == null) return;
            int removeAt = -1;
            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                string other = EditorGUILayout.TextField(c.otherTraitRef);
                float  delta = EditorGUILayout.FloatField(c.opinionDelta, GUILayout.Width(70));
                if (EditorGUI.EndChangeCheck()) { ctx.RecordUndo("修改相性"); list[i] = new TraitCompatibility(other, delta); ctx.MarkDirty(); }
                if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(22))) removeAt = i;
                EditorGUILayout.EndHorizontal();
            }
            if (removeAt >= 0) { ctx.RecordUndo("删除相性"); list.RemoveAt(removeAt); ctx.MarkDirty(); }
            if (GUILayout.Button("+ 添加相性", GUILayout.Width(90))) { ctx.RecordUndo("添加相性"); list.Add(new TraitCompatibility()); ctx.MarkDirty(); }
        }

        private static void AiWeightListEditor(IChronicleEditorContext ctx, List<TraitAiWeight> list)
        {
            if (list == null) return;
            int removeAt = -1;
            for (int i = 0; i < list.Count; i++)
            {
                var w = list[i];
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                string axis   = EditorGUILayout.TextField(w.axisRef);
                float  weight = EditorGUILayout.FloatField(w.weight, GUILayout.Width(70));
                if (EditorGUI.EndChangeCheck()) { ctx.RecordUndo("修改AI权重"); list[i] = new TraitAiWeight(axis, weight); ctx.MarkDirty(); }
                if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(22))) removeAt = i;
                EditorGUILayout.EndHorizontal();
            }
            if (removeAt >= 0) { ctx.RecordUndo("删除AI权重"); list.RemoveAt(removeAt); ctx.MarkDirty(); }
            if (GUILayout.Button("+ 添加AI权重", GUILayout.Width(100))) { ctx.RecordUndo("添加AI权重"); list.Add(new TraitAiWeight()); ctx.MarkDirty(); }
        }
    }
}
