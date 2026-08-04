using System.Collections.Generic;
using Ale.Toolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Ale.Chronicle.Editor
{
    /// <summary>
    /// 「角色」页签（三列）：左=角色模板列表、中=角色列表（模板过滤 / 搜索 / 从模板添加 / 快速添加）、
    /// 右=角色 Inspector（选中角色时）或角色模板 Inspector（选中左列模板时）。布局与选中互斥由基类提供。
    /// </summary>
    public sealed class CharacterSystemTab : EditorThreeColumnTab<CharacterDefinition>
    {
        private readonly CharacterTemplateListPanel _templatePanel = new CharacterTemplateListPanel();
        private readonly CharacterListPanel         _listPanel     = new CharacterListPanel();
        private IEditorMasterListPanel<ChronicleDatabase>[] _leftPanels;

        protected override IEditorMasterListPanel<ChronicleDatabase>[] LeftPanels
            => _leftPanels ??= new IEditorMasterListPanel<ChronicleDatabase>[] { _templatePanel };

        protected override string EntityNoun => "角色";

        protected override List<CharacterDefinition> EntityList(ChronicleDatabase db) => db.Characters;

        protected override CharacterDefinition DrawEntityList(IChronicleEditorContext ctx, CharacterDefinition displaySelected)
            => _listPanel.DrawList(ctx, displaySelected);

        protected override CharacterDefinition ConsumePendingSelect() => _listPanel.ConsumePendingSelect();

        protected override void DrawEntityInspector(IChronicleEditorContext ctx, CharacterDefinition entity)
            => CharacterInspectorPanel.Draw(ctx, entity);
    }

    /// <summary>角色列表面板（中列）：模板过滤 + 搜索 + 从模板添加 / 快速添加，每行 id / 姓名 简要。</summary>
    public sealed class CharacterListPanel : EditorEntityListPanel<CharacterDefinition, CharacterTemplate>
    {
        public CharacterListPanel() : base("ChronicleCharacterListDrag") { }

        protected override EChronicleEntityKind Kind => EChronicleEntityKind.Character;
        protected override string Noun => "角色";

        protected override List<CharacterDefinition> Entities(ChronicleDatabase db) => db.Characters;
        protected override List<CharacterTemplate>   Templates(ChronicleDatabase db) => db.CharacterTemplates;
        protected override string TemplateName(CharacterTemplate t) => t.name;
        protected override string TemplateRefOf(CharacterDefinition e) => e.templateRef;
        protected override string IdOf(CharacterDefinition e) => e.id;

        protected override Color RowDotColor(ChronicleDatabase db, CharacterDefinition e)
        {
            var t = db.GetCharacterTemplate(e.templateRef);
            return t != null ? t.color : Color.gray;
        }

        protected override string SearchName(ChronicleDatabase db, CharacterDefinition e)
            => e.GetAttributeValue<string>(WellKnownAttr.Name);

        protected override CharacterDefinition AddFromTemplate(IChronicleEditorContext ctx, string templateName)
        {
            var db = ctx.Database;
            ctx.RecordUndo("从模板添加角色");
            var c = new CharacterDefinition(GenerateId(db, "char_", id => db.GetCharacter(id) != null), templateName);
            c.RebuildAttributes(db);
            db.Characters.Add(c);
            ctx.MarkDirty();
            return c;
        }

        protected override CharacterDefinition QuickAdd(IChronicleEditorContext ctx)
        {
            var db = ctx.Database;
            if (db.Characters.Count == 0)
                return AddFromTemplate(ctx, db.CharacterTemplates.Count > 0 ? db.CharacterTemplates[0].name : null);

            ctx.RecordUndo("快速添加角色");
            var clone = db.Characters[db.Characters.Count - 1].Clone();
            clone.id = GenerateId(db, "char_", id => db.GetCharacter(id) != null);
            db.Characters.Add(clone);
            ctx.MarkDirty();
            return clone;
        }

        protected override void DrawRowColumns(ChronicleDatabase db, CharacterDefinition e,
            Rect keyRow, float contentX, float contentRight, float valY, float valH)
        {
            float w     = Mathf.Max(0f, contentRight - contentX);
            float idW   = Mathf.Min(140f, w * 0.5f);
            float nameX = contentX + idW + Pad;
            float nameW = Mathf.Max(0f, contentRight - nameX);

            GUI.Label(new Rect(contentX, keyRow.y, idW, keyRow.height), "ID", KeyStyle);
            GUI.Label(new Rect(nameX, keyRow.y, nameW, keyRow.height), "姓名", KeyStyle);

            GUI.Label(new Rect(contentX, valY, idW, valH), string.IsNullOrEmpty(e.id) ? "(空 ID)" : e.id, IdStyle);
            string name = e.GetAttributeValue<string>(WellKnownAttr.Name);
            GUI.Label(new Rect(nameX, valY, nameW, valH), name ?? string.Empty, SubStyle);
        }
    }

    /// <summary>角色模板主列表面板（角色页左列）：绑定 <see cref="ChronicleDatabase.CharacterTemplates"/>；专属字段=生成规则（画在 schema 之后）。</summary>
    public sealed class CharacterTemplateListPanel : ChronicleTemplateListPanel<CharacterTemplate>
    {
        protected override List<CharacterTemplate> GetList(ChronicleDatabase db) => db.CharacterTemplates;
        protected override string Noun => "角色模板";
        protected override string NewNamePrefix => "template_";
        protected override CharacterTemplate NewTemplate(string name) => new CharacterTemplate(name);
        protected override string SchemaLabel => "身份字段 schema";
        protected override bool ExtrasBeforeSchema => false;   // 生成规则画在 schema 之后

        protected override void DrawExtras(IChronicleEditorContext ctx, CharacterTemplate tmpl)
        {
            EditorGUILayout.LabelField("生成规则（预留）", ToolkitEditorStyles.Header);
            EditorGUI.BeginChangeCheck();
            string race   = EditorGUILayout.TextField("默认种族(可空)", tmpl.raceRef);
            int    budget = EditorGUILayout.IntField("属性点预算", tmpl.attributePointBudget);
            int    minAge = EditorGUILayout.IntField("初始年龄下限(世界日)", tmpl.minAgeDays);
            int    maxAge = EditorGUILayout.IntField("初始年龄上限(世界日)", tmpl.maxAgeDays);
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
