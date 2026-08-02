using Ale.Toolkit.Runtime;
using UnityEngine;

namespace Ale.Chronicle
{
    /// <summary>
    /// 编年史运行时管理器（MonoBehaviour 单例；场景中需挂一个，未挂载则 <see cref="Instance"/> 为 null）。
    /// Chronicle 的<b>唯一运行时管理单例</b>，集中承载运行时所需的各项功能——当前分部负责
    /// <b>UI 运行时管理</b>（覆盖式 UI 根节点 / Layer 设置 + 全局技能悬停弹窗的持有与惰性实例化）；
    /// 后续按需扩展运行时配置数据管理、存档数据的 Get / Set / Load / Save 等（各自新增分部）。
    ///
    /// <para>弹窗以接口 <see cref="ISkillTooltip"/> 持有，管理器不依赖 UI 程序集里的具体弹窗组件（依赖倒置）。
    /// 继承 toolkit <see cref="ToolkitMonoSingleton{T}"/>：Awake 设实例 + DontDestroyOnLoad 跨场景持久，
    /// 重复实例时后来者自毁；关闭 Domain Reload 时静态状态也在每次播放开始复位。</para>
    /// </summary>
    public partial class ChronicleRuntimeManager : ToolkitMonoSingleton<ChronicleRuntimeManager>
    {
        // ── UI 运行时管理 · 覆盖式 UI 设置 ─────────────────────────────────────────────

        [Header("覆盖式 UI 设置")]
        [Tooltip("弹窗等覆盖式 UI 的根节点。为空则运行时自动查找场景中首个 Canvas。")]
        [SerializeField] private Transform coverUiRoot;

        [Tooltip("是否将覆盖式 UI（弹窗等）强制设置到下方指定的 Layer。\n" +
                 "当使用独立 UI 摄像机、且其 Culling Mask 仅渲染 UI 层时开启：弹窗会分配独立 Canvas，" +
                 "其 Layer 可能与父级不一致，需在实例化后重新指定，UI 摄像机方可渲染。")]
        [SerializeField] private bool applyCoverUiLayer;

        [Layer]
        [Tooltip("覆盖式 UI 强制设置到的 Layer（如 UI）。仅当上方开关开启时生效。")]
        [SerializeField] private int coverUiLayer;

        /// <summary>设置覆盖式 UI 根节点。</summary>
        public void SetCoverUiRoot(Transform parent) => coverUiRoot = parent;

        /// <summary>设置覆盖式 UI 强制 Layer（同时开启强制开关）。layer 会被约束到 0~31。</summary>
        public void SetCoverUiLayer(int layer)
        {
            coverUiLayer      = CoverUiUtil.ClampLayer(layer);
            applyCoverUiLayer = true;
        }

        /// <summary>按层名设置覆盖式 UI 强制 Layer（同时开启强制开关）。层名不存在则记警告且不改动。</summary>
        public void SetCoverUiLayer(string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer < 0)
            {
                Debug.LogWarning($"[ChronicleRuntimeManager] Layer 名称 \"{layerName}\" 不存在，未设置覆盖式 UI Layer。");
                return;
            }
            SetCoverUiLayer(layer);
        }

        /// <summary>关闭「覆盖式 UI 强制 Layer」（此后不再改动覆盖式 UI 的 Layer）。</summary>
        public void DisableCoverUiLayer() => applyCoverUiLayer = false;

        /// <summary>
        /// 将指定覆盖式 UI 对象（及其所有子级）递归设置到配置的 Layer。未开启强制开关时为无操作。
        /// 递归是必要的：带独立 Canvas 的子级也须落到目标 Layer，仅渲染该 Layer 的 UI 摄像机方可渲染。
        /// </summary>
        public void ApplyCoverUiLayer(GameObject go)
        {
            if (!go || !applyCoverUiLayer) return;
            CoverUiUtil.SetLayerRecursively(go, coverUiLayer);
        }

        // ── UI 运行时管理 · 技能悬停弹窗 ───────────────────────────────────────────────

        [Header("技能悬停弹窗")]
        [Tooltip("技能悬停详情弹窗预制体：运行时由本管理器全局实例化一次。其根节点需实现 ISkillTooltip（如 UiwSkillTooltip）。可空。")]
        [SerializeField] private GameObject skillTooltipPrefab;

        private ISkillTooltip _skillTooltip;
        private bool          _skillTooltipResolved;

        /// <summary>
        /// 全局技能悬停弹窗（首次访问时按 <see cref="skillTooltipPrefab"/> 懒实例化一次）。未配置预制体时为 null。
        /// </summary>
        public ISkillTooltip SkillTooltip => EnsureSkillTooltip();

        private ISkillTooltip EnsureSkillTooltip()
        {
            if (_skillTooltipResolved) return _skillTooltip;
            _skillTooltipResolved = true;

            if (!skillTooltipPrefab) return _skillTooltip = null;

            var parent = coverUiRoot ? coverUiRoot : CoverUiUtil.FindCanvasTransform();
            var go     = parent ? Instantiate(skillTooltipPrefab, parent) : Instantiate(skillTooltipPrefab);
            go.transform.SetAsLastSibling();   // 置于父级最上层渲染
            ApplyCoverUiLayer(go);             // 覆盖式 UI：按需强制到指定 Layer（如 UI）
            _skillTooltip = go.GetComponent<ISkillTooltip>();
            if (_skillTooltip == null)
                Debug.LogWarning("[ChronicleRuntimeManager] skillTooltipPrefab 根节点未实现 ISkillTooltip（如 UiwSkillTooltip），技能悬停弹窗不可用。");
            return _skillTooltip;
        }

        /// <summary>在光标处（屏幕坐标）显示指定技能的悬停详情弹窗（全局统一入口）。</summary>
        public void ShowSkillTooltip(Skill skill, Vector2 screenPos)
            => EnsureSkillTooltip()?.Show(skill, screenPos);

        /// <summary>隐藏（原位淡出）技能悬停弹窗。未实例化时为无操作。</summary>
        public void HideSkillTooltip()
        {
            if (_skillTooltipResolved) _skillTooltip?.Hide();
        }
    }
}
