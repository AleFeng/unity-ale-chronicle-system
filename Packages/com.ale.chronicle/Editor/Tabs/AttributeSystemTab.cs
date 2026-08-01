using System.Collections.Generic;
using Ale.Chronicle;
using Ale.Toolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Ale.Chronicle.Editor
{
    /// <summary>「核心属性」页签：左列核心属性列表（增删/重排），右列检视器（id / 显示名·缩写·图标·说明 / 类别 / min-max / 默认基础值）。</summary>
    public sealed class AttributeSystemTab : ChronicleTwoColumnTab
    {
        private readonly CoreAttributeListPanel _panel = new CoreAttributeListPanel();
        protected override IEditorMasterListPanel<ChronicleDatabase> Panel => _panel;
    }

    /// <summary>核心属性主列表面板：绑定 <see cref="ChronicleDatabase.CoreAttributes"/>，右列检视核心属性各字段。</summary>
    public sealed class CoreAttributeListPanel : EditorMasterListPanel<CoreAttributeDefinition>
    {
        protected override List<CoreAttributeDefinition> GetList(ChronicleDatabase db) => db.CoreAttributes;
        protected override string Noun => "核心属性";

        protected override string RowLabel(CoreAttributeDefinition item)
            => string.IsNullOrEmpty(item.id) ? "(未命名)" : item.PlainName();

        protected override CoreAttributeDefinition CreateNew(ChronicleDatabase db, List<CoreAttributeDefinition> list)
        {
            int n = list.Count + 1;
            string id;
            do { id = "attr_" + n; n++; } while (Contains(list, id));
            var def = new CoreAttributeDefinition(id);
            def.displayName.SetTextValue(0, "新属性");
            return def;
        }

        private static bool Contains(List<CoreAttributeDefinition> list, string id)
        {
            foreach (var d in list) if (d != null && d.id == id) return true;
            return false;
        }

        public override void DrawInspector(IChronicleEditorContext ctx, CoreAttributeDefinition item)
        {
            if (item == null)
            {
                EditorGUILayout.LabelField("请选择或新建一个核心属性。", ToolkitEditorStyles.Placeholder);
                return;
            }

            item.Normalize();

            EditorGUILayout.LabelField("基础信息", ToolkitEditorStyles.Header);

            EditorGUI.BeginChangeCheck();
            string id       = EditorGUILayout.TextField("ID", item.id);
            string category = EditorGUILayout.TextField("类别枚举(可空)", item.categoryEnumRef);
            float  min      = EditorGUILayout.FloatField("下限", item.minValue);
            float  max      = EditorGUILayout.FloatField("上限", item.maxValue);
            float  def      = EditorGUILayout.FloatField("默认基础值", item.defaultBase);
            if (EditorGUI.EndChangeCheck())
            {
                ctx.RecordUndo("修改核心属性");
                item.id              = id;
                item.categoryEnumRef = category;
                item.minValue        = min;
                item.maxValue        = max;
                item.defaultBase     = def;
                ctx.MarkDirty();
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("显示 / 资源", ToolkitEditorStyles.Header);

            // Text / Sprite 属性值：复用统一绘制器（自理 Undo / 标脏 / 本地化 / Addressable）。
            AttributeFieldDrawer.Draw(ctx, "显示名", item.displayName, null);
            AttributeFieldDrawer.Draw(ctx, "缩写",   item.abbreviation, null);
            AttributeFieldDrawer.Draw(ctx, "图标",   item.icon, null);
            AttributeFieldDrawer.Draw(ctx, "说明",   item.description, null);
        }
    }
}
