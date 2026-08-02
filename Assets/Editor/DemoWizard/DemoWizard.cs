using UnityEditor;
using UnityEngine;
using static Ale.Toolkit.Editor.UiPrefabBuilder;
#if ATK_TMP && ATK_LOCALIZATION
using Ale.Toolkit.Runtime.UI;
#endif

namespace Ale.Chronicle.DemoEditor
{
    /// <summary>
    /// Chronicle Demo Wizard —— 一键生成技能 UI 测试预制体（编辑器工具，位于工程 Assets 内，不进 com.ale.chronicle 包）。
    /// 参照 inventory 的 DemoWizard 范式；产物落 <c>Assets/Demo/Assets/UI/Prefab/{Tab,Item,ItemList,Tool,View}/</c>，
    /// 结构对齐 inventory 的 <c>PF_UiwSkill*</c> 参考预制体。菜单入口见 <see cref="DemoWizardWindow"/>。
    ///
    /// <para>复用 toolkit 的 <see cref="Ale.Toolkit.Editor.UiPrefabBuilder"/> / <see cref="Ale.Toolkit.Editor.UiTextBuilder"/>
    /// 原语搭建层级。ATK_TMP 开启时文本节点为 TMP；ATK_LOCALIZATION 开启时每节点挂 LocalizedTextEvent、
    /// 根节点挂 LocalizedFontEvent。</para>
    /// </summary>
    public static partial class ChronicleDemoWizard
    {
        // ── 目录 ──────────────────────────────────────────────────────────────
        private const string DemoRoot = "Assets/Demo";
        private static string PrefabRoot  => DemoRoot + "/Assets/UI/Prefab";
        private static string DataDir     => DemoRoot + "/Data";
        private static string ManagerPath => DemoRoot + "/" + KPfDemoManager + ".prefab";

        // ── 预制体名（统一 PF_组件类名）────────────────────────────────────────
        private const string KPfFilterTabBtn   = "PF_FilterTabBtn";       // 过滤按钮（UiwFilterTabBar 的按钮）→ Tab/
        private const string KPfSkillCell      = "PF_UiwSkillCell";       // 技能网格条目 UiwSkillEntry        → Item/
        private const string KPfSkillDetail    = "PF_UiwSkillDetail";     // 技能列表条目 UiwSkillEntry        → Item/
        private const string KPfSkillGridList  = "PF_UiwSkillGridList";   // 技能网格列表 UiwSkillGridList     → ItemList/
        private const string KPfSkillOrderList = "PF_UiwSkillOrderList";  // 技能顺序列表 UiwSkillOrderList    → ItemList/
        private const string KPfSkillTooltip   = "PF_UiwSkillTooltip";    // 技能悬停弹窗 UiwSkillTooltip      → Tool/
        private const string KPfSkillView      = "PF_UiwSkillView";       // 技能主界面 UiwSkillView          → View/
        private const string KPfDemoManager    = "ChronicleDemoManager";  // 演示宿主（Demo 入口，置于 Demo 根）

        /// <summary>预制体 → 子文件夹（与 Runtime/UI 组件所在子目录一致）。</summary>
        private static string PrefabSubfolder(string name)
        {
            switch (name)
            {
                case KPfFilterTabBtn:   return "Tab";
                case KPfSkillCell:
                case KPfSkillDetail:    return "Item";
                case KPfSkillGridList:
                case KPfSkillOrderList: return "ItemList";
                case KPfSkillTooltip:   return "Tool";
                case KPfSkillView:      return "View";
                default:                return string.Empty;
            }
        }

        /// <summary>预制体资产路径。</summary>
        private static string Pfb(string name)
        {
            if (name == KPfDemoManager) return ManagerPath;   // 宿主置于 Demo 根，非 Prefab 子目录
            string sub = PrefabSubfolder(name);
            return string.IsNullOrEmpty(sub)
                ? PrefabRoot + "/" + name + ".prefab"
                : PrefabRoot + "/" + sub + "/" + name + ".prefab";
        }

        /// <summary>预建全部输出文件夹。</summary>
        private static void EnsureFolders()
        {
            EnsureFolder(DemoRoot);
            EnsureFolder(DataDir);
            EnsureFolder(PrefabRoot);
            EnsureFolder(PrefabRoot + "/Tab");
            EnsureFolder(PrefabRoot + "/Item");
            EnsureFolder(PrefabRoot + "/ItemList");
            EnsureFolder(PrefabRoot + "/Tool");
            EnsureFolder(PrefabRoot + "/View");
        }

        /// <summary>
        /// builder 开头：由预制体名解析目标路径并确保文件夹存在。<b>刻意不删旧资产</b>以保住 GUID
        /// （内容由 <see cref="SavePrefab"/> 整体替换，不会有旧节点残留）。
        /// </summary>
        private static string BeginPrefab(string prefabName)
        {
            string path = Pfb(prefabName);
            int sep = path.LastIndexOf('/');
            if (sep > 0) EnsureFolder(path.Substring(0, sep));
            return path;
        }

        /// <summary>
        /// 保存 Prefab（<see cref="PrefabUtility.SaveAsPrefabAsset(GameObject, string, out bool)"/> 就地覆盖、保 GUID）
        /// 并销毁临时 GameObject。ATK_TMP &amp;&amp; ATK_LOCALIZATION 下先给根节点挂本地化字体事件。
        /// </summary>
        private static void SavePrefab(GameObject root, string path, bool attachFont = true)
        {
#if ATK_TMP && ATK_LOCALIZATION
            if (attachFont) AttachFontEvent(root);
#endif
            MovePrimaryUiwToTop(root);
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
            Object.DestroyImmediate(root);
            if (saved) Debug.Log("[ChronicleDemoWizard] 预制体已保存：" + path);
            else       Debug.LogError("[ChronicleDemoWizard] 预制体保存失败：" + path);
        }

#if ATK_TMP && ATK_LOCALIZATION
        /// <summary>
        /// 在根节点挂 <see cref="LocalizedFontEvent"/>，写入 toolkit 全局本地化字体引用，
        /// 再扫描全部子节点建立字体绑定（须在层级搭好后调用）。
        /// </summary>
        private static void AttachFontEvent(GameObject root)
        {
            var fontEvent = root.AddComponent<LocalizedFontEvent>();
            var localizedFont = Ale.Toolkit.Editor.ToolkitPrefabFonts.LocalizedFont;
            if (localizedFont != null && !localizedFont.IsEmpty)
                fontEvent.AssetReference.SetReference(
                    localizedFont.TableReference, localizedFont.TableEntryReference);
            fontEvent.RefreshComponents();
        }
#endif
    }
}
