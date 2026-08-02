using UnityEngine;

namespace Ale.Chronicle
{
    /// <summary>
    /// 技能悬停详情弹窗的运行时抽象。具体实现为 UI 层的 <c>UiwSkillTooltip</c>。
    ///
    /// <para>定义于运行时（数据 / 逻辑）程序集，作为依赖倒置缝：<see cref="ChronicleRuntimeManager"/>
    /// 以此接口持有并驱动全局唯一的技能悬停弹窗，无需依赖 UI 程序集里的具体弹窗组件类型。</para>
    /// </summary>
    public interface ISkillTooltip
    {
        /// <summary>在光标处（屏幕坐标）显示指定技能的详情弹窗并淡入。</summary>
        void Show(Skill skill, Vector2 screenPos);

        /// <summary>开始原位淡出弹窗（位置保持不变）。</summary>
        void Hide();
    }
}
