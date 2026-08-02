using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;                              // Button
using Ale.Chronicle.Runtime.UI;                    // UiwSkillEntry
using Ale.Toolkit.Editor;                          // UiTextBuilder, ToolkitPrefabFonts
using static Ale.Toolkit.Editor.UiPrefabBuilder;   // LoadPrefabComp

namespace Ale.Chronicle.DemoEditor
{
    /// <summary>生成项编排：条目目录、依赖闭包、单项 / 所选 / 全量生成入口。</summary>
    public static partial class ChronicleDemoWizard
    {
        /// <summary>单个可生成项（供 <see cref="DemoWizardWindow"/> 列表渲染与生成）。</summary>
        public sealed class GenItem
        {
            public string        Key;
            public string        DisplayName;
            public string        AssetPath;
            public string[]      DepKeys;
            public System.Action Build;
            public string        Category;
        }

        // 分类（窗口按此分组显示，顺序即显示顺序）
        public const string CatCommon = "通用";
        public const string CatSkill  = "技能系统";
        public const string CatDemo   = "演示（运行入口）";
        public static readonly string[] Categories = { CatCommon, CatSkill, CatDemo };

        private static List<GenItem> _items;

        /// <summary>全部可生成项（拓扑有序：依赖先于被依赖者）。</summary>
        public static IReadOnlyList<GenItem> Items => _items ??= BuildItems();

        private static List<GenItem> BuildItems() => new List<GenItem>
        {
            new GenItem { Category = CatCommon, Key = "FilterTabBtn", DisplayName = $"过滤按钮 {KPfFilterTabBtn}",
                AssetPath = Pfb(KPfFilterTabBtn), DepKeys = new string[0], Build = () => BuildFilterTabBtnPrefab() },
            new GenItem { Category = CatSkill, Key = "SkillCell", DisplayName = $"技能网格条目 {KPfSkillCell}",
                AssetPath = Pfb(KPfSkillCell), DepKeys = new string[0], Build = () => BuildSkillCellPrefab() },
            new GenItem { Category = CatSkill, Key = "SkillDetail", DisplayName = $"技能列表条目 {KPfSkillDetail}",
                AssetPath = Pfb(KPfSkillDetail), DepKeys = new string[0], Build = () => BuildSkillDetailPrefab() },
            new GenItem { Category = CatSkill, Key = "SkillGridList", DisplayName = $"技能网格列表 {KPfSkillGridList}",
                AssetPath = Pfb(KPfSkillGridList), DepKeys = new[] { "SkillCell" },
                Build = () => BuildSkillGridListPrefab(LoadPrefabComp<UiwSkillEntry>(Pfb(KPfSkillCell))) },
            new GenItem { Category = CatSkill, Key = "SkillOrderList", DisplayName = $"技能顺序列表 {KPfSkillOrderList}",
                AssetPath = Pfb(KPfSkillOrderList), DepKeys = new[] { "SkillDetail" },
                Build = () => BuildSkillOrderListPrefab(LoadPrefabComp<UiwSkillEntry>(Pfb(KPfSkillDetail))) },
            new GenItem { Category = CatSkill, Key = "SkillTooltip", DisplayName = $"技能悬停弹窗 {KPfSkillTooltip}",
                AssetPath = Pfb(KPfSkillTooltip), DepKeys = new string[0], Build = () => BuildSkillTooltipPrefab() },
            new GenItem { Category = CatSkill, Key = "SkillView", DisplayName = $"技能主界面 {KPfSkillView}",
                AssetPath = Pfb(KPfSkillView), DepKeys = new[] { "SkillGridList", "SkillOrderList", "FilterTabBtn" },
                Build = () => BuildSkillViewPrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(Pfb(KPfSkillGridList)),
                    AssetDatabase.LoadAssetAtPath<GameObject>(Pfb(KPfSkillOrderList)),
                    LoadPrefabComp<Button>(Pfb(KPfFilterTabBtn))) },
            new GenItem { Category = CatDemo, Key = "DemoManager", DisplayName = $"演示宿主 {KPfDemoManager}",
                AssetPath = Pfb(KPfDemoManager), DepKeys = new[] { "SkillView", "SkillTooltip" },
                Build = () => BuildDemoManagerPrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(Pfb(KPfSkillView)),
                    AssetDatabase.LoadAssetAtPath<GameObject>(Pfb(KPfSkillTooltip)),
                    AssetDatabase.LoadAssetAtPath<ChronicleDatabase>(DataDir + "/ChronicleDatabase.asset")) },
        };

        /// <summary>生成全部：统一覆盖确认一次，按拓扑序构建。</summary>
        public static void GenerateAll()
        {
            EnsureFolders();
            var all = new List<GenItem>(Items);
            if (!ConfirmOverwrite(all)) return;
            BuildSubset(all);
            EditorUtility.DisplayDialog("生成完成", $"已生成全部预制体：\n{PrefabRoot}/", "OK");
        }

        /// <summary>生成所选：并入各自依赖闭包（保拓扑序），覆盖确认后构建。</summary>
        public static void GenerateSelected(IEnumerable<string> keys)
        {
            EnsureFolders();
            var picked = new HashSet<string>();
            foreach (var k in keys)
            {
                var it = FindItem(k);
                if (it != null) foreach (var d in CollectWithDeps(it)) picked.Add(d.Key);
            }
            var toGen = Items.Where(g => picked.Contains(g.Key)).ToList();
            if (toGen.Count == 0) { EditorUtility.DisplayDialog("提示", "未勾选任何项。", "OK"); return; }
            if (!ConfirmOverwrite(toGen)) return;
            BuildSubset(toGen);
        }

        /// <summary>生成单项：依赖型条目询问是否一并生成子项，再覆盖确认。</summary>
        public static void GenerateItem(string key)
        {
            EnsureFolders();
            var item = FindItem(key);
            if (item == null) return;

            var deps     = CollectWithDeps(item);
            var onlyDeps = deps.Where(d => d != item).ToList();

            List<GenItem> toGen;
            if (onlyDeps.Count > 0)
            {
                int c = EditorUtility.DisplayDialogComplex("依赖提示",
                    $"「{item.DisplayName}」依赖以下子项：\n\n" +
                    string.Join("\n", onlyDeps.Select(d => "· " + d.DisplayName)) +
                    "\n\n是否一并生成这些依赖？",
                    "一并生成", "取消", "仅生成此项");
                if (c == 1) return;
                toGen = c == 0 ? deps : new List<GenItem> { item };
                if (c == 2)
                {
                    var missing = onlyDeps.Where(d => !Exists(d)).ToList();
                    if (missing.Count > 0)
                        Debug.LogWarning("[ChronicleDemoWizard] 缺少依赖：" +
                            string.Join("，", missing.Select(m => m.DisplayName)) + "，相关引用可能为空。");
                }
            }
            else toGen = new List<GenItem> { item };

            if (!ConfirmOverwrite(toGen)) return;
            BuildSubset(toGen);
        }

        private static GenItem FindItem(string key)
        {
            foreach (var i in Items) if (i.Key == key) return i;
            return null;
        }

        private static bool Exists(GenItem it)
            => AssetDatabase.LoadAssetAtPath<Object>(it.AssetPath) != null;

        /// <summary>返回 item 的传递依赖闭包 ∪ 自身，保持 Items 声明序。</summary>
        private static List<GenItem> CollectWithDeps(GenItem it)
        {
            var picked = new HashSet<string>();
            void Visit(GenItem g)
            {
                if (!picked.Add(g.Key)) return;
                foreach (var dk in g.DepKeys)
                {
                    var d = FindItem(dk);
                    if (d != null) Visit(d);
                }
            }
            Visit(it);
            return Items.Where(g => picked.Contains(g.Key)).ToList();
        }

        /// <summary>列出将被覆盖的已存在资产，弹一次确认；无冲突直接放行。</summary>
        private static bool ConfirmOverwrite(IList<GenItem> toGen)
        {
            var existing = toGen.Where(Exists).ToList();
            if (existing.Count == 0) return true;
            string msg = "以下资产已存在，将被覆盖：\n\n" +
                         string.Join("\n", existing.Select(e => "· " + e.DisplayName));
            return EditorUtility.DisplayDialog("覆盖确认", msg, "覆盖", "取消");
        }

        /// <summary>按给定顺序构建（带进度条），末尾保存刷新。</summary>
        private static void BuildSubset(IList<GenItem> toGen)
        {
#if ATK_TMP
            // AddText 已下沉 toolkit；生成前向 UiTextBuilder 注入向导默认字体（字体为 toolkit 全局设定）。
            UiTextBuilder.DefaultTmpFont = () => ToolkitPrefabFonts.DefaultTmpFont;
#endif
            try
            {
                for (int i = 0; i < toGen.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("生成技能 UI 预制体", toGen[i].DisplayName, (float)i / toGen.Count);
                    toGen[i].Build();
                }
                EditorUtility.DisplayProgressBar("生成技能 UI 预制体", "保存并刷新资产数据库...", 0.97f);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
