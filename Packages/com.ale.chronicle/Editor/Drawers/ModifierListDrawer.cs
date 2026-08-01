using System.Collections.Generic;
using Ale.Chronicle;
using Ale.Toolkit.Runtime;
using Ale.Toolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Ale.Chronicle.Editor
{
    /// <summary>
    /// <see cref="ModifierDefinition"/> 列表的可复用 IMGUI 编辑器：逐条编辑 目标属性 / 运算 / 幅度 / 时效 /
    /// 存活天数 / 来源标记 / 叠加，支持增删。目标属性以数据库核心属性 id 下拉给出（含对悬空值的保留）。
    /// 直接编辑普通对象，Undo/标脏经 <see cref="IChronicleEditorContext"/>。
    /// </summary>
    public static class ModifierListDrawer
    {
        public static void Draw(IChronicleEditorContext ctx, string label, List<ModifierDefinition> list, ChronicleDatabase db)
        {
            EditorGUILayout.LabelField(label, ToolkitEditorStyles.Header);
            if (list == null) return;

            int removeAt = -1;
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                if (m == null) continue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"修饰器 {i + 1}", EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(22))) removeAt = i;
                EditorGUILayout.EndHorizontal();

                EditorGUI.BeginChangeCheck();
                string target  = AttrIdPopup("目标属性", m.targetAttributeId, db);
                var    op      = (EModifierOperation)EditorGUILayout.EnumPopup("运算", m.operation);
                float  mag     = EditorGUILayout.FloatField("幅度", m.magnitude);
                var    dur     = (EModifierDuration)EditorGUILayout.EnumPopup("时效", m.duration);
                float  durDays = m.durationDays;
                if (dur == EModifierDuration.Timed)
                    durDays = EditorGUILayout.FloatField("存活天数", m.durationDays);
                string src     = EditorGUILayout.TextField("来源标记(可空)", m.sourceTag);
                int    limit   = EditorGUILayout.IntField("最大层数", m.stackLimit);
                var    rule    = (EStackRule)EditorGUILayout.EnumPopup("叠加规则", m.stackRule);
                if (EditorGUI.EndChangeCheck())
                {
                    ctx.RecordUndo("修改修饰器");
                    m.targetAttributeId = target;
                    m.operation         = op;
                    m.magnitude         = mag;
                    m.duration          = dur;
                    m.durationDays      = durDays;
                    m.sourceTag         = src;
                    m.stackLimit        = limit;
                    m.stackRule         = rule;
                    ctx.MarkDirty();
                }

                EditorGUILayout.EndVertical();
            }

            if (removeAt >= 0)
            {
                ctx.RecordUndo("删除修饰器");
                list.RemoveAt(removeAt);
                ctx.MarkDirty();
            }

            if (GUILayout.Button("+ 添加修饰器"))
            {
                ctx.RecordUndo("添加修饰器");
                list.Add(new ModifierDefinition());
                ctx.MarkDirty();
            }
        }

        /// <summary>目标属性 id 下拉（源自数据库核心属性）；无核心属性时退化为文本框，悬空值保留在列表首位。</summary>
        private static string AttrIdPopup(string label, string current, ChronicleDatabase db)
        {
            var ids = new List<string>();
            if (db != null)
                foreach (var a in db.CoreAttributes)
                    if (a != null && !string.IsNullOrEmpty(a.id) && !ids.Contains(a.id))
                        ids.Add(a.id);

            if (ids.Count == 0)
                return EditorGUILayout.TextField(label, current);

            if (!string.IsNullOrEmpty(current) && !ids.Contains(current))
                ids.Insert(0, current);   // 保留悬空引用，避免下拉把它清掉

            int idx    = Mathf.Max(0, ids.IndexOf(current));
            int newIdx = EditorGUILayout.Popup(label, idx, ids.ToArray());
            return ids[newIdx];
        }
    }
}
