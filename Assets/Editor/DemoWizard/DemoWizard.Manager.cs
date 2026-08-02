using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Ale.Chronicle.Demo;                          // ChronicleDemoBootstrap
using static Ale.Toolkit.Editor.UiPrefabBuilder;

namespace Ale.Chronicle.DemoEditor
{
    /// <summary>演示宿主预制体的构建：Canvas + ChronicleRuntimeManager（悬停宿主）+ SkillView 实例 + 数据库引导，拖入场景即可 Play。</summary>
    public static partial class ChronicleDemoWizard
    {
        /// <summary>
        /// 构建 ChronicleDemoManager（Demo 入口宿主）：根挂 <see cref="ChronicleRuntimeManager"/> +
        /// <see cref="ChronicleDemoBootstrap"/>；子 Canvas（覆盖式）+ EventSystem + SkillView 实例。
        /// </summary>
        static void BuildDemoManagerPrefab(GameObject skillViewPrefab, GameObject skillTooltipPrefab, ChronicleDatabase db)
        {
            string path = ManagerPath;
            int sep = path.LastIndexOf('/');
            if (sep > 0) EnsureFolder(path.Substring(0, sep));

            var root = NewGameObject(KPfDemoManager);   // 纯 Transform 根（非 UI）
            var mgr  = root.AddComponent<ChronicleRuntimeManager>();
            var boot = root.AddComponent<ChronicleDemoBootstrap>();
            boot.database = db;
            if (!db) Debug.LogWarning("[ChronicleDemoWizard] 未找到 " + DataDir + "/ChronicleDatabase.asset，运行时技能目录为空。");

            // Canvas（覆盖式，覆盖整屏）
            var canvasGo = ChildGameObject("Canvas", root.transform);
            canvasGo.AddComponent<RectTransform>();
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();

            // EventSystem（悬停 / 点击 / 拖拽所需；若场景已有一个，保留其一即可）
            var esGo = ChildGameObject("EventSystem", root.transform);
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<StandaloneInputModule>();

            // ChronicleRuntimeManager 接线（私有序列化字段，走 SerializedObject）
            SetSerializedRef(mgr, "skillTooltipPrefab", skillTooltipPrefab);
            SetSerializedRef(mgr, "coverUiRoot", canvasGo.transform);
            if (!skillTooltipPrefab) Debug.LogWarning("[ChronicleDemoWizard] 缺少 PF_UiwSkillTooltip，悬停详情不可用。");

            // SkillView 实例（Canvas 下居中）
            if (skillViewPrefab)
            {
                var viewInst = (GameObject)PrefabUtility.InstantiatePrefab(skillViewPrefab, canvasGo.transform);
                var vrt = (RectTransform)viewInst.transform;
                vrt.anchorMin = vrt.anchorMax = vrt.pivot = new Vector2(0.5f, 0.5f);
                vrt.anchoredPosition = Vector2.zero;
            }
            else Debug.LogWarning("[ChronicleDemoWizard] 缺少 PF_UiwSkillView。");

            // 根挂管理器 / 引导（非 Uiw 组件、含嵌套预制体实例），直接保存（不走 SavePrefab 的字体事件 / 置顶）。
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
            Object.DestroyImmediate(root);
            if (saved) Debug.Log("[ChronicleDemoWizard] 预制体已保存：" + path);
            else       Debug.LogError("[ChronicleDemoWizard] 预制体保存失败：" + path);
        }
    }
}
