using UnityEditor;
using UnityEngine;
using Ale.Chronicle.Runtime.UI;

namespace Ale.Chronicle.Editor
{
    /// <summary>
    /// <see cref="UiwSkillView"/> 的自定义 Inspector：按当前「技能来源」<see cref="ESkillSource"/> 条件显示配置字段——
    /// Character 来源显示「角色 ID」；Database 来源显示数据库全部技能、无需额外配置。
    /// 其余字段（标题 / 列表 / 视图切换 / 分组页签 / 搜索）照常绘制。
    /// </summary>
    [CustomEditor(typeof(UiwSkillView))]
    public class UiwSkillViewInspector : UnityEditor.Editor
    {
        private SerializedProperty _source;
        private SerializedProperty _characterId;

        private void OnEnable()
        {
            _source      = serializedObject.FindProperty("source");
            _characterId = serializedObject.FindProperty("characterId");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Script 条目（只读；自定义 Inspector 默认不显示，此处手动补上）
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));

            // 技能来源 + 按来源仅显示对应配置
            EditorGUILayout.PropertyField(_source);
            if ((ESkillSource)_source.enumValueIndex == ESkillSource.Character)
                EditorGUILayout.PropertyField(_characterId,
                    new GUIContent("角色 ID", "显示该角色（SkillRuntimeManager）当前已学会的技能。"));
            // Database：显示数据库全部技能，无需额外配置。

            // 其余字段（含继承的 titleLabel 相关）照常绘制，排除已在上方按来源单独处理的字段。
            DrawPropertiesExcluding(serializedObject, "m_Script", "source", "characterId");

            serializedObject.ApplyModifiedProperties();
        }
    }
}
