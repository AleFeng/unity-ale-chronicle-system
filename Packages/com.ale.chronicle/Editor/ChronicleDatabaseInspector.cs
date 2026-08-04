using UnityEditor;
using UnityEngine;

namespace Ale.Chronicle.Editor
{
    /// <summary>
    /// <see cref="ChronicleDatabase"/> 资产的自定义 Inspector：顶部一个醒目的「打开编年史编辑器」按钮，
    /// 一键在 <see cref="ChronicleEditorWindow"/> 中载入该数据库进行可视化编辑；下方保留默认 Inspector 以便查看原始数据。
    /// </summary>
    [CustomEditor(typeof(ChronicleDatabase))]
    public sealed class ChronicleDatabaseInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var db = (ChronicleDatabase)target;

            EditorGUILayout.Space(4);
            using (new EditorGUI.DisabledScope(!db))
            {
                if (GUILayout.Button("在 编年史编辑器 中进行编辑", GUILayout.Height(30)))
                    ChronicleEditorWindow.Open(db);
            }
            EditorGUILayout.HelpBox("推荐通过上方编辑器窗口进行配置；下方为原始数据视图。", MessageType.None);
            EditorGUILayout.Space(6);

            DrawDefaultInspector();
        }
    }
}
