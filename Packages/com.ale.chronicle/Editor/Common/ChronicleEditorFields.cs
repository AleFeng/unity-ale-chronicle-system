using System.Collections.Generic;
using Ale.Toolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Ale.Chronicle.Editor
{
    /// <summary>
    /// 编年史检视器的可复用 IMGUI 字段绘制助手（跨页签下沉）：字符串 id 列表、分组单选下拉、
    /// 核心属性 id 下拉、内联 <c>ConditionExpression</c> 属性。均直接编辑普通对象，Undo / 标脏经
    /// <see cref="IChronicleEditorContext"/>；由调用方负责在其上绘制分区标题（Header / miniBoldLabel）。
    /// </summary>
    public static class ChronicleEditorFields
    {
        /// <summary>
        /// 字符串 id 列表编辑器：逐行文本框 + <c>✕</c> 删除（延迟应用）+ 底部添加按钮。
        /// Undo 文案统一为 <c>"修改"/"删除"/"添加" + undoNoun</c>；调用方自绘上方标题。
        /// </summary>
        public static void StringList(IChronicleEditorContext ctx, List<string> list, string undoNoun,
            string addLabel = "+ 添加", float addWidth = 90f)
        {
            if (list == null) return;
            int removeAt = -1;
            for (int i = 0; i < list.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                string v = EditorGUILayout.TextField(list[i]);
                if (EditorGUI.EndChangeCheck()) { ctx.RecordUndo("修改" + undoNoun); list[i] = v; ctx.MarkDirty(); }
                if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(22))) removeAt = i;
                EditorGUILayout.EndHorizontal();
            }
            if (removeAt >= 0) { ctx.RecordUndo("删除" + undoNoun); list.RemoveAt(removeAt); ctx.MarkDirty(); }
            if (GUILayout.Button(addLabel, GUILayout.Width(addWidth))) { ctx.RecordUndo("添加" + undoNoun); list.Add(""); ctx.MarkDirty(); }
        }

        /// <summary>
        /// 分组标签单选下拉（首项「（无）」= 清空）：从统一 <see cref="ChronicleDatabase.GroupTags"/> 池取值。
        /// 无分组标签时退化为提示行；选中变更经 <paramref name="apply"/> 回写（<c>null</c>=无分组）。
        /// </summary>
        public static void GroupPopup(IChronicleEditorContext ctx, string current, string undo, System.Action<string> apply)
        {
            var db = ctx.Database;
            if (db.GroupTags.Count == 0)
            {
                EditorGUILayout.LabelField("分组", "（无分组标签；在「通用 → 分组标签」添加）");
                return;
            }
            var ids    = new List<string> { "" };
            var labels = new List<string> { "（无）" };
            foreach (var g in db.GroupTags)
                if (g != null && !string.IsNullOrEmpty(g.id)) { ids.Add(g.id); labels.Add(g.PlainName()); }

            int idx = Mathf.Max(0, ids.IndexOf(current ?? ""));
            EditorGUI.BeginChangeCheck();
            int newIdx = EditorGUILayout.Popup("分组", idx, labels.ToArray());
            if (EditorGUI.EndChangeCheck())
            {
                ctx.RecordUndo(undo);
                apply(newIdx <= 0 ? null : ids[newIdx]);
                ctx.MarkDirty();
            }
        }

        /// <summary>目标属性 id 下拉（源自数据库核心属性）；无核心属性时退化为文本框，悬空值保留在列表首位。</summary>
        public static string CoreAttrPopup(string label, string current, ChronicleDatabase db)
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

        /// <summary>
        /// 内联 <c>ConditionExpression</c> 属性：经数据库 <c>SerializedObject</c> 定位
        /// <paramref name="listProp"/>[<paramref name="indexOf"/>()].<paramref name="relProp"/>，
        /// 交由 Ale.Condition 的 <c>[CustomPropertyDrawer]</c> 渲染。调用方自绘上方标题。
        /// <paramref name="indexOf"/> 惰性求值：仅在序列化对象 / 数据库就绪后才解析条目下标。
        /// </summary>
        public static void InlineCondition(IChronicleEditorContext ctx, string listProp, System.Func<int> indexOf, string relProp)
        {
            var so = ctx.Serialized;
            if (so == null || ctx.Database == null)
            {
                EditorGUILayout.HelpBox("条件编辑暂不可用（无序列化对象）。", MessageType.None);
                return;
            }
            so.Update();
            var arr = so.FindProperty(listProp);
            int idx = indexOf();
            if (arr == null || idx < 0 || idx >= arr.arraySize)
            {
                EditorGUILayout.HelpBox("条件编辑暂不可用。", MessageType.None);
                return;
            }
            var prop = arr.GetArrayElementAtIndex(idx).FindPropertyRelative(relProp);
            if (prop == null) return;
            EditorGUILayout.PropertyField(prop, new GUIContent("条件"), true);
            so.ApplyModifiedProperties();
        }
    }
}
