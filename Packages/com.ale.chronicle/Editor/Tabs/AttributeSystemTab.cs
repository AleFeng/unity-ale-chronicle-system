using System.Collections.Generic;
using Ale.Toolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Ale.Chronicle.Editor
{
    /// <summary>
    /// 「属性」页签（三列）：左=属性模板列表、中=核心属性列表（模板过滤 / 搜索 / 从模板添加 / 快速添加）、
    /// 右=属性 Inspector（选中属性时）或属性模板 Inspector（选中左列模板时）。
    /// </summary>
    public sealed class AttributeSystemTab : EditorThreeColumnTab<CoreAttributeDefinition>
    {
        private readonly CoreAttributeTemplatePanel _templatePanel = new CoreAttributeTemplatePanel();
        private readonly CoreAttributeListPanel     _listPanel     = new CoreAttributeListPanel();
        private IEditorMasterListPanel<ChronicleDatabase>[] _leftPanels;

        protected override IEditorMasterListPanel<ChronicleDatabase>[] LeftPanels
            => _leftPanels ??= new IEditorMasterListPanel<ChronicleDatabase>[] { _templatePanel };

        protected override string EntityNoun => "核心属性";

        protected override List<CoreAttributeDefinition> EntityList(ChronicleDatabase db) => db.CoreAttributes;

        protected override CoreAttributeDefinition DrawEntityList(IChronicleEditorContext ctx, CoreAttributeDefinition displaySelected)
            => _listPanel.DrawList(ctx, displaySelected);

        protected override CoreAttributeDefinition ConsumePendingSelect() => _listPanel.ConsumePendingSelect();

        protected override void DrawEntityInspector(IChronicleEditorContext ctx, CoreAttributeDefinition entity)
            => CoreAttributeInspectorPanel.Draw(ctx, entity);
    }

    /// <summary>核心属性列表面板（中列）：模板过滤 + 搜索 + 从模板添加 / 快速添加，每行 id / 名称 / 范围·基础。</summary>
    public sealed class CoreAttributeListPanel : EditorEntityListPanel<CoreAttributeDefinition, CoreAttributeTemplate>
    {
        public CoreAttributeListPanel() : base("ChronicleCoreAttrListDrag") { }

        protected override EChronicleEntityKind Kind => EChronicleEntityKind.CoreAttribute;
        protected override string Noun => "核心属性";

        protected override List<CoreAttributeDefinition> Entities(ChronicleDatabase db) => db.CoreAttributes;
        protected override List<CoreAttributeTemplate>   Templates(ChronicleDatabase db) => db.CoreAttributeTemplates;
        protected override string TemplateName(CoreAttributeTemplate t) => t.name;
        protected override string TemplateRefOf(CoreAttributeDefinition e) => e.templateRef;
        protected override string IdOf(CoreAttributeDefinition e) => e.id;

        protected override Color RowDotColor(ChronicleDatabase db, CoreAttributeDefinition e)
        {
            var t = db.GetCoreAttributeTemplate(e.templateRef);
            return t != null ? t.color : Color.gray;
        }

        protected override string SearchName(ChronicleDatabase db, CoreAttributeDefinition e) => e.PlainName();

        protected override CoreAttributeDefinition AddFromTemplate(IChronicleEditorContext ctx, string templateName)
        {
            var db = ctx.Database;
            ctx.RecordUndo("从模板添加核心属性");
            var t = db.GetCoreAttributeTemplate(templateName);
            var a = new CoreAttributeDefinition(GenerateId(db, "attr_", id => db.GetCoreAttribute(id) != null))
            {
                templateRef = templateName,
            };
            if (t != null)
            {
                a.categoryEnumRef = t.categoryEnumRef;
                a.minValue        = t.minValue;
                a.maxValue        = t.maxValue;
                a.defaultBase     = t.defaultBase;
            }
            a.displayName.SetTextValue(0, "新属性");
            a.RebuildAttributes(db);
            db.CoreAttributes.Add(a);
            ctx.MarkDirty();
            return a;
        }

        protected override CoreAttributeDefinition QuickAdd(IChronicleEditorContext ctx)
        {
            var db = ctx.Database;
            if (db.CoreAttributes.Count == 0)
                return AddFromTemplate(ctx, db.CoreAttributeTemplates.Count > 0 ? db.CoreAttributeTemplates[0].name : null);

            ctx.RecordUndo("快速添加核心属性");
            var clone = db.CoreAttributes[db.CoreAttributes.Count - 1].Clone();
            clone.id = GenerateId(db, "attr_", id => db.GetCoreAttribute(id) != null);
            db.CoreAttributes.Add(clone);
            ctx.MarkDirty();
            return clone;
        }

        protected override void DrawRowColumns(ChronicleDatabase db, CoreAttributeDefinition e,
            Rect keyRow, float contentX, float contentRight, float valY, float valH)
        {
            float w     = Mathf.Max(0f, contentRight - contentX);
            float idW   = Mathf.Min(110f, w * 0.32f);
            float numW  = Mathf.Min(130f, w * 0.34f);
            float nameX = contentX + idW + Pad;
            float numX  = contentRight - numW;
            float nameW = Mathf.Max(0f, numX - Pad - nameX);

            GUI.Label(new Rect(contentX, keyRow.y, idW, keyRow.height), "ID", KeyStyle);
            GUI.Label(new Rect(nameX, keyRow.y, nameW, keyRow.height), "名称", KeyStyle);
            GUI.Label(new Rect(numX, keyRow.y, numW, keyRow.height), "范围·基础", KeyStyle);

            GUI.Label(new Rect(contentX, valY, idW, valH), string.IsNullOrEmpty(e.id) ? "(空 ID)" : e.id, IdStyle);
            GUI.Label(new Rect(nameX, valY, nameW, valH), e.PlainName(), SubStyle);
            GUI.Label(new Rect(numX, valY, numW, valH), $"{e.minValue:0.#}~{e.maxValue:0.#}·{e.defaultBase:0.#}", SubStyle);
        }
    }

    /// <summary>属性模板主列表面板（属性页左列）：绑定 <see cref="ChronicleDatabase.CoreAttributeTemplates"/>；专属字段=默认类别 / 区间 / 基础值。</summary>
    public sealed class CoreAttributeTemplatePanel : ChronicleTemplateListPanel<CoreAttributeTemplate>
    {
        protected override List<CoreAttributeTemplate> GetList(ChronicleDatabase db) => db.CoreAttributeTemplates;
        protected override string Noun => "属性模板";
        protected override string NewNamePrefix => "attr_template_";
        protected override CoreAttributeTemplate NewTemplate(string name) => new CoreAttributeTemplate(name);
        protected override string SchemaLabel => "自定义属性字段 schema";

        protected override void DrawExtras(IChronicleEditorContext ctx, CoreAttributeTemplate tmpl)
        {
            EditorGUI.BeginChangeCheck();
            string category = EditorGUILayout.TextField("默认类别枚举(可空)", tmpl.categoryEnumRef);
            float  min = EditorGUILayout.FloatField("默认下限", tmpl.minValue);
            float  max = EditorGUILayout.FloatField("默认上限", tmpl.maxValue);
            float  def = EditorGUILayout.FloatField("默认基础值", tmpl.defaultBase);
            if (EditorGUI.EndChangeCheck())
            {
                ctx.RecordUndo("修改属性模板");
                tmpl.categoryEnumRef = category;
                tmpl.minValue        = min;
                tmpl.maxValue        = max;
                tmpl.defaultBase     = def;
                ctx.MarkDirty();
            }
        }
    }

    /// <summary>核心属性实例检视器（属性页右列）：ID / 来源模板 / 显示·资源 / 类别·区间·基础 / 自定义字段。</summary>
    public static class CoreAttributeInspectorPanel
    {
        public static void Draw(IChronicleEditorContext ctx, CoreAttributeDefinition item)
        {
            if (item == null)
            {
                EditorGUILayout.LabelField("请选择或新建一个核心属性。", ToolkitEditorStyles.Placeholder);
                return;
            }
            item.Normalize();

            EditorGUILayout.LabelField("基础信息", ToolkitEditorStyles.Header);
            ChronicleEntityHeader.DrawIdField(ctx, "核心属性", item.id,
                ctx.DuplicateIdsOf(EChronicleEntityKind.CoreAttribute), v => item.id = v);
            ChronicleEntityHeader.DrawTemplateRefReadonly(item.templateRef);

            EditorGUI.BeginChangeCheck();
            string category = EditorGUILayout.TextField("类别枚举(可空)", item.categoryEnumRef);
            float  min      = EditorGUILayout.FloatField("下限", item.minValue);
            float  max      = EditorGUILayout.FloatField("上限", item.maxValue);
            float  def      = EditorGUILayout.FloatField("默认基础值", item.defaultBase);
            if (EditorGUI.EndChangeCheck())
            {
                ctx.RecordUndo("修改核心属性");
                item.categoryEnumRef = category;
                item.minValue        = min;
                item.maxValue        = max;
                item.defaultBase     = def;
                ctx.MarkDirty();
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("显示 / 资源", ToolkitEditorStyles.Header);
            AttributeFieldDrawer.Draw(ctx, "显示名", item.displayName, null);
            AttributeFieldDrawer.Draw(ctx, "缩写",   item.abbreviation, null);
            AttributeFieldDrawer.Draw(ctx, "图标",   item.icon, null);
            AttributeFieldDrawer.Draw(ctx, "说明",   item.description, null);

            EditorGUILayout.Space(4);
            var tmpl = ctx.Database.GetCoreAttributeTemplate(item.templateRef);
            ChronicleEntityHeader.DrawCustomAttributes(ctx, item.values, tmpl?.attributes, "（无——模板未定义自定义属性字段）");
        }
    }
}
