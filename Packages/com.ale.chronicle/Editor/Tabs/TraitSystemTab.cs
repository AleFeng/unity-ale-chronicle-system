using System.Collections.Generic;
using Ale.Chronicle;
using Ale.Toolkit.Runtime;
using Ale.Toolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Ale.Chronicle.Editor
{
    /// <summary>「特质」页签：左列特质列表（行首标注 永久/临时），右列检视器
    /// （基础信息 / 显示·资源 / 遗传·出生 / 修饰器 / 互斥 / 相性 / AI 权重 / 获得条件）。</summary>
    public sealed class TraitSystemTab : ChronicleTwoColumnTab
    {
        private readonly TraitListPanel _panel = new TraitListPanel();
        protected override IEditorMasterListPanel<ChronicleDatabase> Panel => _panel;
    }

    /// <summary>特质主列表面板：绑定 <see cref="ChronicleDatabase.Traits"/> + 完整特质检视器。</summary>
    public sealed class TraitListPanel : EditorMasterListPanel<TraitDefinition>
    {
        protected override List<TraitDefinition> GetList(ChronicleDatabase db) => db.Traits;
        protected override string Noun => "特质";

        protected override string RowLabel(TraitDefinition item)
        {
            string name = string.IsNullOrEmpty(item.id) ? "(未命名)" : item.PlainName();
            return (item.IsTemporary ? "[临时] " : "[永久] ") + name;
        }

        protected override TraitDefinition CreateNew(ChronicleDatabase db, List<TraitDefinition> list)
        {
            int n = list.Count + 1;
            string id;
            do { id = "trait_" + n; n++; } while (Contains(list, id));
            var t = new TraitDefinition(id);
            t.displayName.SetTextValue(0, "新特质");
            return t;
        }

        private static bool Contains(List<TraitDefinition> list, string id)
        {
            foreach (var t in list) if (t != null && t.id == id) return true;
            return false;
        }

        public override void DrawInspector(IChronicleEditorContext ctx, TraitDefinition trait)
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
            EditorGUI.BeginChangeCheck();
            string id       = EditorGUILayout.TextField("ID", trait.id);
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
                trait.id                    = id;
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
