using System;
using System.Collections.Generic;
using Ale.Toolkit.Runtime;
using Ale.Toolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Ale.Chronicle.Editor
{
    /// <summary>
    /// 实体 Inspector 的共用绘制片段：ID 行（重复/空 ID 红色高亮 + 提示）、只读「来源模板」行。
    /// 三列页签的中列实体经「从模板添加」设定模板，Inspector 里模板只读、ID 可改。仿 inventory <c>EditorEntityHeader</c>。
    /// </summary>
    public static class ChronicleEntityHeader
    {
        /// <summary>「⚠ ID 重复或为空」的默认提示文案。</summary>
        public const string DefaultDuplicateHint = "⚠ ID 重复或为空";

        /// <summary>ID 输入行：重复 / 空 ID 红框高亮 + 下方提示；改动经 <paramref name="setId"/> 写回（内部 RecordUndo + MarkDirty）。</summary>
        public static void DrawIdField(IChronicleEditorContext ctx, string noun, string currentId,
            ICollection<string> duplicateIds, Action<string> setId, string dupHint = DefaultDuplicateHint)
        {
            bool isDup = duplicateIds != null && duplicateIds.Contains(
                string.IsNullOrWhiteSpace(currentId) ? string.Empty : currentId);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("ID");
            EditorGUI.BeginChangeCheck();
            string newId = EditorGUILayout.TextField(
                currentId, isDup ? ToolkitEditorStyles.RedField : EditorStyles.textField);
            if (EditorGUI.EndChangeCheck())
            {
                ctx.RecordUndo($"修改{noun} ID");
                setId(newId);
                ctx.MarkDirty();
            }
            EditorGUILayout.EndHorizontal();

            if (isDup)
                EditorGUILayout.LabelField(dupHint, ToolkitEditorStyles.StatusError);
        }

        /// <summary>只读「来源模板」行（创建后不可更改；为空显示「（无）」）。</summary>
        public static void DrawTemplateRefReadonly(string templateRef)
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.TextField("来源模板", string.IsNullOrEmpty(templateRef) ? "（无）" : templateRef);
        }

        /// <summary>
        /// 「自定义属性」区：逐条绘制实例来自模板 schema 的属性值（枚举字段按 <paramref name="templateAttrs"/> 解析枚举类型）。
        /// </summary>
        public static void DrawCustomAttributes(IChronicleEditorContext ctx,
            List<AttributeEntry> values, List<AttributeDefinition> templateAttrs, string emptyHint)
        {
            EditorGUILayout.LabelField("自定义属性字段（来自模板 schema）", ToolkitEditorStyles.Header);

            if (values == null || values.Count == 0)
            {
                EditorGUILayout.LabelField(emptyHint, ToolkitEditorStyles.Placeholder);
                return;
            }

            Dictionary<string, AttributeDefinition> defs = null;
            if (templateAttrs != null && templateAttrs.Count > 0)
            {
                defs = new Dictionary<string, AttributeDefinition>(templateAttrs.Count);
                foreach (var d in templateAttrs)
                    if (d != null && !string.IsNullOrEmpty(d.id)) defs[d.id] = d;
            }

            var db = ctx.Database;
            foreach (var entry in values)
            {
                if (entry == null || entry.value == null) continue;
                AttributeDefinition def = null;
                if (defs != null && !string.IsNullOrEmpty(entry.id)) defs.TryGetValue(entry.id, out def);
                var enumType = def != null && def.type == EFieldType.Enum ? db.GetEnumType(def.enumTypeRef) : null;
                AttributeFieldDrawer.Draw(ctx, entry.id, entry.value, enumType);
            }
        }
    }
}
