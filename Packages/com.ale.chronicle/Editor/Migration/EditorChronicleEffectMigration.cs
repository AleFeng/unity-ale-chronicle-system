using Ale.Effect;
using Ale.Effect.Editor;
using UnityEditor;
using UnityEngine;

namespace Ale.Chronicle.Editor
{
    /// <summary>
    /// 0.4.0 → 0.5.0 迁移窗口：把 <see cref="ChronicleDatabase"/> 里 legacy 的效果 / Gameplay 标签搬进 toolkit 效果库 <see cref="EffectDatabase"/>
    /// （可就地新建）。同 id 冲突跳过并报告、不覆盖（冲突条目留在 legacy 字段，处理后可重跑）；两资产标脏保存，并刷新效果目录。
    /// 纯数据部分在运行时程序集的 <see cref="ChronicleLegacyEffects"/>（可测试）。
    /// </summary>
    public sealed class EditorChronicleEffectMigration : EditorWindow
    {
        private ChronicleDatabase _source;
        private EffectDatabase    _target;
        private string            _report;
        private Vector2           _scroll;

        [MenuItem("Tools/Ale Toolkit/Chronicle System/迁移效果到 Effect Database", priority = 1102)]
        public static void Open() => Open(Selection.activeObject as ChronicleDatabase);

        /// <summary>打开迁移窗口并预填来源库（为空时取当前选中 / 编年史编辑器最近打开的库）。</summary>
        public static void Open(ChronicleDatabase source)
        {
            var w = GetWindow<EditorChronicleEffectMigration>(true, "迁移效果到 Effect Database");
            w.minSize = new Vector2(560f, 380f);
            if (source) w._source = source;
            else if (!w._source)
                w._source = AssetDatabase.LoadAssetAtPath<ChronicleDatabase>(EditorPrefs.GetString("ChronicleSystem.DatabasePath", string.Empty));
            w._report = null;
            w.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "0.5.0 起效果与 Gameplay 标签由 toolkit 效果库（EffectDatabase）承载，所有上层系统共用；编年史库只保留技能的效果 id 引用。\n" +
                "本工具把 0.4.0 存在编年史库里的效果 / 标签逐条搬入目标效果库：显示名 / 描述 / 图标 / 定义原样保留（不挂模板）；" +
                "目标库已有同 id 的效果跳过并报告（不覆盖，条目留在 legacy 字段）；标签按名称去重并入。", MessageType.Info);

            EditorGUILayout.Space(4);
            _source = (ChronicleDatabase)EditorGUILayout.ObjectField("来源（编年史库）", _source, typeof(ChronicleDatabase), false);
            if (_source)
            {
#pragma warning disable 618
                int fx = _source.LegacyEffects.Count, tags = _source.LegacyGameplayTags.Count;
#pragma warning restore 618
                EditorGUILayout.LabelField($"legacy 效果 {fx} 条，legacy Gameplay 标签 {tags} 条", EditorStyles.miniLabel);
            }

            EditorGUILayout.BeginHorizontal();
            _target = (EffectDatabase)EditorGUILayout.ObjectField("目标（效果库）", _target, typeof(EffectDatabase), false);
            if (GUILayout.Button("新建…", GUILayout.Width(60)))
            {
                var created = EffectEditorWindow.CreateDatabaseAsset();
                if (created) _target = created;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);
            bool canRun = _source && _target && _source.HasLegacyEffectData;
            using (new EditorGUI.DisabledScope(!canRun))
            {
                if (GUILayout.Button("迁移", GUILayout.Height(28))) Run();
            }
            if (_source && !_source.HasLegacyEffectData)
                EditorGUILayout.LabelField("来源库没有 legacy 效果 / 标签数据，无需迁移。", EditorStyles.miniLabel);

            if (!string.IsNullOrEmpty(_report))
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("结果", EditorStyles.boldLabel);
                _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(80f));
                EditorGUILayout.LabelField(_report, EditorStyles.wordWrappedLabel);
                EditorGUILayout.EndScrollView();
                if (_target && GUILayout.Button("在 Effect Editor 中打开目标库"))
                    EffectEditorWindow.Open(_target);
            }
        }

        private void Run()
        {
            Undo.RecordObject(_target, "迁移效果到 Effect Database");
            Undo.RecordObject(_source, "迁移效果到 Effect Database");
            var report = ChronicleLegacyEffects.MigrateInto(_source, _target, removeFromLegacy: true);
            EditorUtility.SetDirty(_target);
            EditorUtility.SetDirty(_source);
            AssetDatabase.SaveAssets();
            EffectEditorCatalog.Rebuild();
            _report = report.ToString();
            if (report.HasConflicts)
                _report += "\n\n存在同 id 冲突：请在目标库中重命名 / 删除对应效果后重跑（冲突条目仍留在来源库的 legacy 字段）。";
            Debug.Log("[ChronicleEffectMigration] " + _report.Replace('\n', ' '));
        }
    }
}
