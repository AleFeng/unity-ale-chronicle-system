using UnityEditor;
using UnityEngine;

namespace Ale.Chronicle.Editor
{
    /// <summary>
    /// <see cref="ChronicleDatabase"/> 资产的自定义 Inspector：顶部「打开编年史编辑器」按钮；若资产仍含 0.4.0 legacy 效果 / Gameplay 标签，
    /// 给出提示与「迁移效果到 Effect Database」入口；下方保留默认 Inspector 以便查看原始数据。
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

            if (db && db.HasLegacyEffectData)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox("本资产仍含 0.4.0 存于编年史库的效果 / Gameplay 标签（legacy）。0.5.0 起效果由 toolkit 效果库（EffectDatabase）承载，" +
                                        "运行时不再读取这些数据；请迁移到效果库。", MessageType.Warning);
                if (GUILayout.Button("迁移效果到 Effect Database…", GUILayout.Height(24)))
                    EditorChronicleEffectMigration.Open(db);
            }
            EditorGUILayout.Space(6);

            DrawDefaultInspector();
        }
    }
}
