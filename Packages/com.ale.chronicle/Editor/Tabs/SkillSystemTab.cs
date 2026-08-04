using System.Collections.Generic;
using Ale.Toolkit.Runtime;
using Ale.Toolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Ale.Chronicle.Editor
{
    /// <summary>
    /// 「技能」页签（三列）：左=技能模板列表、中=技能列表（模板过滤 / 搜索 / 从模板添加 / 快速添加）、
    /// 右=技能 Inspector（选中技能时）或技能模板 Inspector（选中左列模板时）。
    /// 分组标签复用统一 <see cref="ChronicleDatabase.GroupTags"/> 池（在「通用 → 分组标签」管理），故左列不设分组标签面板。
    /// </summary>
    public sealed class SkillSystemTab : EditorThreeColumnTab<Skill>
    {
        private readonly SkillTemplatePanel _templatePanel = new SkillTemplatePanel();
        private readonly SkillListPanel     _listPanel     = new SkillListPanel();
        private IEditorMasterListPanel<ChronicleDatabase>[] _leftPanels;

        protected override IEditorMasterListPanel<ChronicleDatabase>[] LeftPanels
            => _leftPanels ??= new IEditorMasterListPanel<ChronicleDatabase>[] { _templatePanel };

        protected override string EntityNoun => "技能";

        protected override List<Skill> EntityList(ChronicleDatabase db) => db.Skills;

        protected override Skill DrawEntityList(IChronicleEditorContext ctx, Skill displaySelected)
            => _listPanel.DrawList(ctx, displaySelected);

        protected override Skill ConsumePendingSelect() => _listPanel.ConsumePendingSelect();

        protected override void DrawEntityInspector(IChronicleEditorContext ctx, Skill entity)
            => SkillInspectorPanel.Draw(ctx, entity);
    }

    /// <summary>技能列表面板（中列）：模板过滤 + 搜索 + 从模板添加 / 快速添加，每行 id / 名称 / 描述 / 主分组。</summary>
    public sealed class SkillListPanel : EditorEntityListPanel<Skill, SkillTemplate>
    {
        public SkillListPanel() : base("ChronicleSkillListDrag") { }

        protected override EChronicleEntityKind Kind => EChronicleEntityKind.Skill;
        protected override string Noun => "技能";

        protected override List<Skill>         Entities(ChronicleDatabase db)  => db.Skills;
        protected override List<SkillTemplate> Templates(ChronicleDatabase db) => db.SkillTemplates;
        protected override string TemplateName(SkillTemplate t) => t.name;
        protected override string TemplateRefOf(Skill e) => e.templateRef;
        protected override string IdOf(Skill e) => e.id;

        protected override Color RowDotColor(ChronicleDatabase db, Skill e)
        {
            var t = db.GetSkillTemplate(e.templateRef);
            return t != null ? t.color : Color.gray;
        }

        protected override bool Matches(ChronicleDatabase db, Skill e, string term)
        {
            if (string.IsNullOrEmpty(term)) return true;
            term = term.ToLowerInvariant();
            if (!string.IsNullOrEmpty(e.id) && e.id.ToLowerInvariant().Contains(term)) return true;
            string name = e.displayText != null ? e.displayText.GetTextValue() : null;
            return !string.IsNullOrEmpty(name) && name.ToLowerInvariant().Contains(term);
        }

        protected override Skill AddFromTemplate(IChronicleEditorContext ctx, string templateName)
        {
            var db = ctx.Database;
            ctx.RecordUndo("从模板添加技能");
            var skill = new Skill(GenerateId(db, "skill_", id => db.GetSkill(id) != null), templateName);

            // 从模板复制「技能默认信息」（名称 / 描述 / 图标 / 分组标签）；自定义属性值由 RebuildAttributes 依模板 schema 初始化。
            var t = db.GetSkillTemplate(templateName);
            if (t != null)
            {
                skill.displayText        = t.displayText     != null ? t.displayText.Clone()     : new AttributeValue(EFieldType.Text);
                skill.descriptionText    = t.descriptionText != null ? t.descriptionText.Clone() : new AttributeValue(EFieldType.Text);
                skill.iconValue          = t.iconValue != null ? t.iconValue.Clone() : new AttributeValue(EFieldType.Sprite);
                skill.primaryGroupTag    = t.primaryGroupTag;
                skill.secondaryGroupTags = new List<string>(t.secondaryGroupTags);
            }
            skill.RebuildAttributes(db);
            db.Skills.Add(skill);
            ctx.MarkDirty();
            return skill;
        }

        protected override Skill QuickAdd(IChronicleEditorContext ctx)
        {
            var db = ctx.Database;
            if (db.Skills.Count == 0)
                return AddFromTemplate(ctx, db.SkillTemplates.Count > 0 ? db.SkillTemplates[0].name : null);

            ctx.RecordUndo("快速添加技能");
            var clone = db.Skills[db.Skills.Count - 1].Clone();
            clone.id = GenerateId(db, "skill_", id => db.GetSkill(id) != null);
            db.Skills.Add(clone);
            ctx.MarkDirty();
            return clone;
        }

        protected override void DrawRowColumns(ChronicleDatabase db, Skill e,
            Rect keyRow, float contentX, float contentRight, float valY, float valH)
        {
            float w     = Mathf.Max(0f, contentRight - contentX);
            float idW   = Mathf.Min(90f, w * 0.26f);
            float grpW  = Mathf.Min(84f, w * 0.22f);
            float nameX = contentX + idW + Pad;
            float grpX  = contentRight - grpW;
            float nameW = Mathf.Max(0f, (grpX - Pad - nameX) * 0.42f);
            float descX = nameX + nameW + Pad;
            float descW = Mathf.Max(0f, grpX - Pad - descX);

            GUI.Label(new Rect(contentX, keyRow.y, idW,   keyRow.height), "ID",   KeyStyle);
            GUI.Label(new Rect(nameX,    keyRow.y, nameW, keyRow.height), "名称", KeyStyle);
            GUI.Label(new Rect(descX,    keyRow.y, descW, keyRow.height), "描述", KeyStyle);
            GUI.Label(new Rect(grpX,     keyRow.y, grpW,  keyRow.height), "主分组", KeyStyle);

            GUI.Label(new Rect(contentX, valY, idW, valH), string.IsNullOrEmpty(e.id) ? "(空 ID)" : e.id, IdStyle);

            string name = e.displayText != null ? e.displayText.GetTextValue() : null;
            GUI.Label(new Rect(nameX, valY, nameW, valH), string.IsNullOrEmpty(name) ? "—" : name, SubStyle);

            string desc = e.descriptionText != null ? e.descriptionText.GetTextValue() : null;
            GUI.Label(new Rect(descX, valY, descW, valH), string.IsNullOrEmpty(desc) ? "—" : desc, SubStyle);

            var    grp     = db.GetGroupTag(e.primaryGroupTag);
            string grpName = grp != null ? grp.PlainName() : "—";
            GUI.Label(new Rect(grpX, valY, grpW, valH), grpName, SubStyle);
        }
    }

    /// <summary>技能模板主列表面板（技能页左列）：绑定 <see cref="ChronicleDatabase.SkillTemplates"/>；专属字段=技能默认信息 + 分组标签。</summary>
    public sealed class SkillTemplatePanel : ChronicleTemplateListPanel<SkillTemplate>
    {
        protected override List<SkillTemplate> GetList(ChronicleDatabase db) => db.SkillTemplates;
        protected override string Noun => "技能模板";
        protected override string NewNamePrefix => "skill_template_";
        protected override SkillTemplate NewTemplate(string name) => new SkillTemplate(name);
        protected override string SchemaLabel => "自定义属性字段 schema";

        protected override void DrawExtras(IChronicleEditorContext ctx, SkillTemplate tmpl)
        {
            EditorGUILayout.LabelField("技能默认信息（从模板创建时复制）", ToolkitEditorStyles.Header);
            SkillConfigDrawer.DrawDisplayFields(ctx, tmpl);
            EditorGUILayout.Space(6);
            SkillConfigDrawer.DrawGroupTags(ctx, tmpl);
        }
    }

    /// <summary>技能实例检视器（技能页右列）：ID / 显示信息 / 来源模板 / 分组标签 / 自定义属性字段。</summary>
    public static class SkillInspectorPanel
    {
        public static void Draw(IChronicleEditorContext ctx, Skill skill)
        {
            if (skill == null)
            {
                EditorGUILayout.LabelField("请选择或新建一个技能。", ToolkitEditorStyles.Placeholder);
                return;
            }

            EditorGUILayout.LabelField("基础信息", ToolkitEditorStyles.Header);
            ChronicleEntityHeader.DrawIdField(ctx, "技能", skill.id,
                ctx.DuplicateIdsOf(EChronicleEntityKind.Skill), v => skill.id = v);

            // 名称 / 描述 / 图标（与技能模板共用绘制）
            SkillConfigDrawer.DrawDisplayFields(ctx, skill);
            ChronicleEntityHeader.DrawTemplateRefReadonly(skill.templateRef);

            EditorGUILayout.Space(6);
            SkillConfigDrawer.DrawGroupTags(ctx, skill);

            EditorGUILayout.Space(6);
            var tmpl = ctx.Database.GetSkillTemplate(skill.templateRef);
            ChronicleEntityHeader.DrawCustomAttributes(ctx, skill.values, tmpl?.attributes,
                "（该技能暂无自定义属性字段；可在左侧「技能模板」中添加）");
        }
    }
}
