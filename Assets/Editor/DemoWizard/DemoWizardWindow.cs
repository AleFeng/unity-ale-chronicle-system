using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ale.Chronicle.DemoEditor
{
    /// <summary>
    /// Chronicle Demo Wizard 窗口：勾选需要生成的技能 UI 预制体后「生成所选」（自动补齐依赖），
    /// 或用每项右侧「生成」单独生成。菜单：Tools/Ale Toolkit/Chronicle System/Demo Wizard。
    /// </summary>
    public class DemoWizardWindow : EditorWindow
    {
        private Vector2 _scroll;
        private readonly Dictionary<string, bool> _selected   = new Dictionary<string, bool>();
        private readonly Dictionary<string, bool> _catFoldout = new Dictionary<string, bool>();

        [MenuItem("Tools/Ale Toolkit/Chronicle System/Demo Wizard", priority = 1102)]
        public static void Open()
        {
            var w = GetWindow<DemoWizardWindow>(true, "Chronicle Demo Wizard", true);
            w.minSize = new Vector2(460f, 420f);
            w.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Chronicle Demo Wizard · 技能 UI 预制体生成", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "勾选需要生成的预制体，点「生成所选」（自动补齐依赖）；或用每项右侧「生成」单独生成。\n" +
                "产物落 Assets/Demo/Assets/UI/Prefab/{Tab,Item,ItemList,Tool,View}/，结构对齐 inventory 参考预制体。",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("生成全部", GUILayout.Height(24)))
                    ChronicleDemoWizard.GenerateAll();
                if (GUILayout.Button("生成所选", GUILayout.Height(24)))
                    ChronicleDemoWizard.GenerateSelected(
                        _selected.Where(kv => kv.Value).Select(kv => kv.Key).ToList());
                if (GUILayout.Button("全选", GUILayout.Width(56), GUILayout.Height(24)))
                    foreach (var it in ChronicleDemoWizard.Items) _selected[it.Key] = true;
                if (GUILayout.Button("清空", GUILayout.Width(56), GUILayout.Height(24)))
                    _selected.Clear();
            }

            EditorGUILayout.Space(4);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, EditorStyles.helpBox);
            foreach (var cat in ChronicleDemoWizard.Categories)
            {
                var items = ChronicleDemoWizard.Items.Where(it => it.Category == cat).ToList();
                if (items.Count == 0) continue;

                bool open = !_catFoldout.TryGetValue(cat, out bool stored) || stored;  // 默认展开
                open = EditorGUILayout.Foldout(open, $"{cat}（{items.Count}）", true);
                _catFoldout[cat] = open;
                if (!open) continue;

                EditorGUI.indentLevel++;
                foreach (var it in items)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _selected.TryGetValue(it.Key, out bool sel);
                        sel = EditorGUILayout.ToggleLeft(it.DisplayName, sel);
                        _selected[it.Key] = sel;
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("生成", GUILayout.Width(56)))
                            ChronicleDemoWizard.GenerateItem(it.Key);
                    }
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
