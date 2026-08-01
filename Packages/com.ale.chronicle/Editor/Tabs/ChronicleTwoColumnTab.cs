using Ale.Chronicle;
using Ale.Toolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Ale.Chronicle.Editor
{
    /// <summary>
    /// 两列系统页签基类：左列一个主列表面板（<see cref="IEditorMasterListPanel{ChronicleDatabase}"/>，
    /// 增删/重排/选中），右列该面板对选中项的 Inspector。用于「核心属性」「枚举」等无模板/实体分层的扁平列表。
    /// </summary>
    public abstract class ChronicleTwoColumnTab : IEditorSystemTab<ChronicleDatabase>
    {
        private const float LeftWidth = 300f, Padding = 4f;

        private int     _selected = -1;
        private Vector2 _leftScroll, _rightScroll;

        /// <summary>左列主列表面板。</summary>
        protected abstract IEditorMasterListPanel<ChronicleDatabase> Panel { get; }

        public void OnGUI(Rect rect, IEditorDbContext<ChronicleDatabase> ctx)
        {
            var leftRect  = new Rect(rect.x + Padding, rect.y + Padding, LeftWidth, rect.height - Padding * 2);
            var rightRect = new Rect(leftRect.xMax + Padding, rect.y + Padding,
                rect.width - LeftWidth - Padding * 3, rect.height - Padding * 2);

            GUILayout.BeginArea(leftRect, EditorStyles.helpBox);
            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);
            _selected = Panel.DrawMasterList(ctx, _selected);
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();

            GUILayout.BeginArea(rightRect, EditorStyles.helpBox);
            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll, false, false,
                GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none);
            Panel.DrawInspectorAt(ctx, _selected);
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        public void OnDatabaseChanged(IEditorDbContext<ChronicleDatabase> ctx)
        {
            _selected = -1;
            Panel.Invalidate();
        }

        public void OnUndoRedo() => Panel.Invalidate();
    }
}
