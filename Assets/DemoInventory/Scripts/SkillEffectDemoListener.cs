using System.Text;
using UnityEngine;
using Ale.Inventory.Runtime;   // InventoryRuntimeManager / ItemUseEvent

namespace Ale.Chronicle.Inventory
{
    /// <summary>
    /// 技能施放 / 道具使用结果的<b>示例</b>订阅端：订阅 <see cref="SkillRuntimeManager.OnSkillUsed"/> 与
    /// <see cref="InventoryRuntimeManager.OnItemUsed"/>，把逐效果施加结果打到 Console。
    /// 自 Chronicle 0.4.0 起效果由效果系统真正执行（属性 / 特质 / 头衔…），本组件只做观测，不再承担「占位效果」。
    ///
    /// <para><see cref="InventoryRuntimeManager"/> 是场景内 Mono 单例、可能晚于本组件创建，故在 Update 里补订阅一次。</para>
    /// </summary>
    public class SkillEffectDemoListener : MonoBehaviour
    {
        private bool _invSubscribed;

        private void OnEnable()
        {
            var mgr = SkillRuntimeManager.Instance;
            if (mgr != null) mgr.OnSkillUsed += HandleSkillUsed;
            TrySubscribeInventory();
        }

        private void OnDisable()
        {
            var mgr = SkillRuntimeManager.Instance;
            if (mgr != null) mgr.OnSkillUsed -= HandleSkillUsed;
            if (_invSubscribed && InventoryRuntimeManager.Instance)
                InventoryRuntimeManager.Instance.OnItemUsed -= HandleItemUsed;
            _invSubscribed = false;
        }

        private void Update()
        {
            if (!_invSubscribed) TrySubscribeInventory();
        }

        private void TrySubscribeInventory()
        {
            var inv = InventoryRuntimeManager.Instance;
            if (!inv) return;
            inv.OnItemUsed += HandleItemUsed;
            _invSubscribed  = true;
        }

        private void HandleSkillUsed(SkillUseEvent e)
        {
            Debug.Log($"[技能施放] 对 '{e.TargetCharacterId}' 施放 '{e.SkillId}'（来源 '{e.SourceKey}'）：{e.EffectsApplied}/{e.EffectResults.Count} 个效果生效 {Describe(e.EffectResults)}", this);
        }

        private void HandleItemUsed(ItemUseEvent e)
        {
            Debug.Log($"[道具使用] 背包 '{e.InventoryId}' 道具 '{e.ItemId}' → 目标 '{e.TargetSubject}'：{e.Result.Outcome}，扣减={e.Result.Consumed} {Describe(e.Result.EffectResults)}", this);
        }

        private static string Describe(System.Collections.Generic.IReadOnlyList<Ale.Effect.EffectApplyResult> results)
        {
            if (results == null || results.Count == 0) return string.Empty;
            var sb = new StringBuilder("（");
            for (int i = 0; i < results.Count; i++)
            {
                if (i > 0) sb.Append("，");
                sb.Append(results[i].Outcome);
            }
            return sb.Append("）").ToString();
        }
    }
}
