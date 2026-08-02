using UnityEngine;

namespace Ale.Chronicle.Inventory
{
    /// <summary>
    /// 「技能施放」效果的<b>示例</b>订阅端（模式二 · U-A）。订阅 <see cref="SkillRuntimeManager.OnSkillUsed"/>，
    /// 在此处执行真正的效果（治疗 / 加 buff / 播特效等）。本示例仅打印日志作为占位——
    /// Chronicle 本阶段无运行时可变角色状态（HP / 法力 / buff），完整效果引擎为后置独立工程。
    ///
    /// <para>把本组件放到整合 Demo 场景中即可观察 <see cref="ConsumableSkillUse"/> 的施放派发。</para>
    /// </summary>
    public class SkillEffectDemoListener : MonoBehaviour
    {
        private void OnEnable()
        {
            var mgr = SkillRuntimeManager.Instance;
            if (mgr != null) mgr.OnSkillUsed += HandleSkillUsed;
        }

        private void OnDisable()
        {
            var mgr = SkillRuntimeManager.Instance;
            if (mgr != null) mgr.OnSkillUsed -= HandleSkillUsed;
        }

        private void HandleSkillUsed(SkillUseEvent e)
        {
            // TODO(业务层)：在此按 e.SkillId 执行真实效果，作用于 e.TargetCharacterId。
            Debug.Log($"[技能施放·占位效果] 对 '{e.TargetCharacterId}' 施放技能 '{e.SkillId}'（来源 '{e.SourceKey}'）。", this);
        }
    }
}
