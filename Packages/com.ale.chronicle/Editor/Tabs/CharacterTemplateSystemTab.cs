using System.Collections.Generic;
using Ale.Chronicle;
using Ale.Toolkit.Runtime;
using Ale.Toolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Ale.Chronicle.Editor
{
    /// <summary>「角色模板」页签：左列模板列表（色点 + 名称），右列检视器
    /// （名称 / 色点 / 身份字段 schema / 生成规则预留）。模板决定新建角色的自由字段结构。</summary>
    public sealed class CharacterTemplateSystemTab : ChronicleTwoColumnTab
    {
        private readonly CharacterTemplateListPanel _panel = new CharacterTemplateListPanel();
        protected override IEditorMasterListPanel<ChronicleDatabase> Panel => _panel;
    }

    /// <summary>角色模板主列表面板：绑定 <see cref="ChronicleDatabase.CharacterTemplates"/> + 模板检视器。</summary>
    public sealed class CharacterTemplateListPanel : EditorMasterListPanel<CharacterTemplate>
    {
        private readonly AttributeDefinitionListDrawer _schemaDrawer = new AttributeDefinitionListDrawer();

        protected override List<CharacterTemplate> GetList(ChronicleDatabase db) => db.CharacterTemplates;
        protected override string Noun => "角色模板";
        protected override bool   HasColorDot => true;
        protected override Color  RowColor(CharacterTemplate item) => item.color;

        protected override string RowLabel(CharacterTemplate item)
            => string.IsNullOrEmpty(item.name) ? "(未命名)" : item.name;

        protected override CharacterTemplate CreateNew(ChronicleDatabase db, List<CharacterTemplate> list)
        {
            int n = list.Count + 1;
            string name;
            do { name = "template_" + n; n++; } while (Contains(list, name));
            return new CharacterTemplate(name);
        }

        private static bool Contains(List<CharacterTemplate> list, string name)
        {
            foreach (var t in list) if (t != null && t.name == name) return true;
            return false;
        }

        protected override void OnInvalidate() => _schemaDrawer.Invalidate();

        public override void DrawInspector(IChronicleEditorContext ctx, CharacterTemplate tmpl)
        {
            if (tmpl == null)
            {
                EditorGUILayout.LabelField("请选择或新建一个角色模板。", ToolkitEditorStyles.Placeholder);
                return;
            }

            // ── 基础信息 ──────────────────────────────────────────────────────────
            EditorGUILayout.LabelField("基础信息", ToolkitEditorStyles.Header);
            EditorGUI.BeginChangeCheck();
            string name = EditorGUILayout.TextField("名称(引用键)", tmpl.name);
            Color  color = EditorGUILayout.ColorField("列表色点", tmpl.color);
            if (EditorGUI.EndChangeCheck())
            {
                ctx.RecordUndo("修改角色模板");
                tmpl.name  = name;
                tmpl.color = color;
                ctx.MarkDirty();
            }

            // ── 身份字段 schema（决定角色自由字段结构）──────────────────────────────
            EditorGUILayout.Space(4);
            _schemaDrawer.Draw(ctx, ctx.Database, tmpl.attributes, "身份字段 schema");

            // ── 生成规则（预留，本阶段不消费）───────────────────────────────────────
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("生成规则（预留）", ToolkitEditorStyles.Header);
            EditorGUI.BeginChangeCheck();
            string race     = EditorGUILayout.TextField("默认种族(可空)", tmpl.raceRef);
            int    budget   = EditorGUILayout.IntField("属性点预算", tmpl.attributePointBudget);
            int    minAge   = EditorGUILayout.IntField("初始年龄下限(世界日)", tmpl.minAgeDays);
            int    maxAge   = EditorGUILayout.IntField("初始年龄上限(世界日)", tmpl.maxAgeDays);
            if (EditorGUI.EndChangeCheck())
            {
                ctx.RecordUndo("修改模板生成规则");
                tmpl.raceRef              = race;
                tmpl.attributePointBudget = budget;
                tmpl.minAgeDays           = minAge;
                tmpl.maxAgeDays           = maxAge;
                ctx.MarkDirty();
            }

            EditorGUILayout.LabelField("出生必带特质 id", EditorStyles.miniBoldLabel);
            StringListEditor(ctx, "必带特质", tmpl.guaranteedTraitRefs);
            EditorGUILayout.LabelField("随机特质候选池 id", EditorStyles.miniBoldLabel);
            StringListEditor(ctx, "候选特质", tmpl.randomTraitPoolRefs);
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
            if (GUILayout.Button("+ 添加" + noun, GUILayout.Width(100))) { ctx.RecordUndo("添加" + noun); list.Add(""); ctx.MarkDirty(); }
        }
    }
}
