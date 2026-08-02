using UnityEngine;
using UnityEngine.UI;
using Ale.Chronicle.Runtime.UI;
using static Ale.Toolkit.Editor.UiPrefabBuilder;
using static Ale.Toolkit.Editor.UiTextBuilder;

namespace Ale.Chronicle.DemoEditor
{
    /// <summary>技能系统预制体（过滤按钮 / 条目 / 列表 / Tooltip / 技能面板）的构建。移植自 inventory DemoWizard，改绑 chronicle 组件。</summary>
    public static partial class ChronicleDemoWizard
    {
        /// <summary>构建 PF_FilterTabBtn（分组页签按钮：Button + Image + Label(TMP)；由 UiwFilterTabBar 逐标签实例化）。</summary>
        static void BuildFilterTabBtnPrefab()
        {
            string path = BeginPrefab(KPfFilterTabBtn);

            var root = NewGameObject(KPfFilterTabBtn);
            SetRectSize(root.AddComponent<RectTransform>(), 72f, 28f);
            var img = root.AddComponent<Image>();
            img.color = Hex("292936");
            var btn = root.AddComponent<Button>();
            btn.targetGraphic = img;
            SetButtonColors(btn, Hex("292936"), Hex("3A3A55"), Hex("1E1E2C"));

            // 标签（UiwFilterTabBar 运行时按分组显示名改写此文本）
            var labelGo = ChildGameObject("Label", root.transform);
            Stretch(labelGo.AddComponent<RectTransform>());
            AddText(labelGo, "全部", 12, Color.white);

            SavePrefab(root, path);
        }

        /// <summary>构建 PF_UiwSkillCell（技能网格条目：位阶背景框 + 图标 + 名称，支持悬停弹窗）。</summary>
        static void BuildSkillCellPrefab()
        {
            string path = BeginPrefab(KPfSkillCell);

            var root = NewGameObject(KPfSkillCell);
            SetRectSize(root.AddComponent<RectTransform>(), 72f, 72f);
            root.AddComponent<Image>().color = new Color(0.14f, 0.14f, 0.20f, 0.90f); // 兼作悬停射线目标

            var entry = root.AddComponent<UiwSkillEntry>();
            entry.rankAttrId           = "位阶";
            entry.rankBackgroundAttrId = "背景框";
            entry.fallbackToId         = true;
            entry.showTooltip          = true;

            // 位阶背景框（铺满整格，位于图标之下；无位阶数据时运行时自动隐藏）
            var rankGo = ChildGameObject("RankBackground", root.transform);
            Stretch(rankGo.AddComponent<RectTransform>());
            var rankImg = rankGo.AddComponent<Image>();
            rankImg.color = Color.white; rankImg.raycastTarget = false; rankImg.enabled = false;
            entry.rankBackground = rankImg;

            // 图标（顶部居中，略内缩）
            var iconGo = ChildGameObject("Icon", root.transform);
            var iconRt = iconGo.AddComponent<RectTransform>();
            iconRt.anchorMin = iconRt.anchorMax = iconRt.pivot = new Vector2(0.5f, 1f);
            iconRt.anchoredPosition = new Vector2(0f, -6f);
            iconRt.sizeDelta = new Vector2(44f, 44f);
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.color = Color.white; iconImg.preserveAspect = true; iconImg.raycastTarget = false;
            entry.iconImage = iconImg;

            // 名称（底部）
            var nameGo = ChildGameObject("NameText", root.transform);
            var nameRt = nameGo.AddComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0f, 0f); nameRt.anchorMax = new Vector2(1f, 0f);
            nameRt.pivot = new Vector2(0.5f, 0f);
            nameRt.sizeDelta = new Vector2(-4f, 18f);
            nameRt.anchoredPosition = new Vector2(0f, 2f);
            var nameTxt = AddText(nameGo, "技能名", 10, Color.white);
            SetSerializedRef(entry, "nameText", nameTxt);

            SavePrefab(root, path);
        }

        /// <summary>构建 PF_UiwSkillDetail（技能列表条目：图标(含位阶背景框) + 名称 + 描述，支持悬停弹窗）。</summary>
        static void BuildSkillDetailPrefab()
        {
            string path = BeginPrefab(KPfSkillDetail);

            var root = NewGameObject(KPfSkillDetail);
            SetRectSize(root.AddComponent<RectTransform>(), 320f, 60f);
            SetLayoutElement(root, minH: 60, prefH: 60, flexW: 1);
            root.AddComponent<Image>().color = new Color(0.14f, 0.14f, 0.20f, 0.85f);
            SetHlg(root, new RectOffset(6, 6, 4, 4), 8f, TextAnchor.MiddleLeft, true, true, false, false);

            var entry = root.AddComponent<UiwSkillEntry>();
            entry.rankAttrId           = "位阶";
            entry.rankBackgroundAttrId = "背景框";
            entry.fallbackToId         = true;
            entry.showTooltip          = true;

            // 图标容器（位阶背景框 + 图标）
            var iconRoot = ChildGameObject("IconRoot", root.transform);
            iconRoot.AddComponent<RectTransform>();
            SetLayoutElement(iconRoot, minW: 48, prefW: 48, minH: 48, prefH: 48);

            var rankGo = ChildGameObject("RankBackground", iconRoot.transform);
            Stretch(rankGo.AddComponent<RectTransform>());
            var rankImg = rankGo.AddComponent<Image>();
            rankImg.color = Color.white; rankImg.raycastTarget = false; rankImg.enabled = false;
            entry.rankBackground = rankImg;

            var iconGo = ChildGameObject("Icon", iconRoot.transform);
            var iconRt = iconGo.AddComponent<RectTransform>();
            iconRt.anchorMin = Vector2.zero; iconRt.anchorMax = Vector2.one;
            iconRt.offsetMin = new Vector2(4f, 4f); iconRt.offsetMax = new Vector2(-4f, -4f);
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.color = Color.white; iconImg.preserveAspect = true; iconImg.raycastTarget = false;
            entry.iconImage = iconImg;

            // 文本块（名称 + 描述，纵向）
            var textCol = ChildGameObject("TextColumn", root.transform);
            textCol.AddComponent<RectTransform>();
            SetLayoutElement(textCol, flexW: 1, minH: 48, prefH: 48);
            SetVlg(textCol, new RectOffset(0, 0, 2, 2), 2f, TextAnchor.UpperLeft, true, true, true, false);

            var nameGo = ChildGameObject("NameText", textCol.transform);
            nameGo.AddComponent<RectTransform>();
            SetLayoutElement(nameGo, minH: 20, prefH: 20);
            var nameTxt = AddText(nameGo, "技能名", 14, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            SetSerializedRef(entry, "nameText", nameTxt);

            var descGo = ChildGameObject("DescText", textCol.transform);
            descGo.AddComponent<RectTransform>();
            SetLayoutElement(descGo, flexH: 1, minH: 20, prefH: 22);
            var descTxt = AddText(descGo, "技能描述", 11, new Color(0.72f, 0.72f, 0.80f), TextAnchor.UpperLeft);
            SetSerializedRef(entry, "descText", descTxt);

            SavePrefab(root, path);
        }
    }
}
