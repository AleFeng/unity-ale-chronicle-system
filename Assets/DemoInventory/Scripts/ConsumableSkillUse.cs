using UnityEngine;
using Ale.Inventory.Runtime;   // InventoryDataManager / InventoryRuntimeManager
using Ale.Toolkit.Runtime;     // EFieldType

namespace Ale.Chronicle.Inventory
{
    /// <summary>
    /// 使用消耗品「触发」技能（模式二 · U-A 薄派发）——Chronicle × Inventory 整合层。
    ///
    /// <para>业务流程：读道具上的「使用技能」属性（<paramref name="onUseSkillAttrId"/>，默认 <c>"使用技能"</c>，
    /// String / String 数组，值为 <b>Chronicle 技能 ID</b>）→ 对目标角色调用
    /// <see cref="SkillRuntimeManager.UseSkill"/> 施放（校验存在→派发 <see cref="SkillRuntimeManager.OnSkillUsed"/>）→
    /// 至少成功施放一个则从背包扣减 1 个该道具（<see cref="InventoryRuntimeManager.TryRemoveItemById"/>）。</para>
    ///
    /// <para>真正的效果（治疗 / 加 buff 等）由业务层订阅 <see cref="SkillRuntimeManager.OnSkillUsed"/> 实现
    /// （示例见 <see cref="SkillEffectDemoListener"/>）——Chronicle 本阶段不内置任何效果执行。
    /// 不改 Inventory 包：目标角色由业务层指定，扣减复用现成 API。</para>
    /// </summary>
    public static class ConsumableSkillUse
    {
        /// <summary>
        /// 对 <paramref name="targetCharacterId"/> 使用 <paramref name="inventoryId"/> 中的 <paramref name="itemId"/>：
        /// 施放该道具「使用技能」属性上配置的技能；至少成功施放一个则扣减 1 个道具。返回是否至少施放成功一个。
        /// </summary>
        public static bool Use(string inventoryId, string itemId, string targetCharacterId, string onUseSkillAttrId)
        {
            if (string.IsNullOrEmpty(itemId) || string.IsNullOrEmpty(targetCharacterId)) return false;

            var dm = InventoryDataManager.Instance;
            var av = dm != null ? dm.GetItem(itemId)?.GetAttributeValue(onUseSkillAttrId) : null;
            if (av == null || av.Type != EFieldType.String) return false;

            var ids = av.StringArray;
            if (ids == null) return false;

            var skillMgr = SkillRuntimeManager.Instance;
            if (skillMgr == null) return false;

            bool any = false;
            foreach (var skillId in ids)
                if (!string.IsNullOrEmpty(skillId))
                    any |= skillMgr.UseSkill(targetCharacterId, skillId, sourceKey: itemId);

            // 仅在确有技能被施放时消耗道具（技能无效 / 配置为空则不浪费道具）。
            if (any)
            {
                var inv = InventoryRuntimeManager.Instance;   // MonoBehaviour 单例，场景中须存在
                if (inv != null) inv.TryRemoveItemById(inventoryId, itemId, 1);
            }
            return any;
        }
    }

    /// <summary>
    /// <see cref="ConsumableSkillUse"/> 的组件封装，便于把「使用」入口挂到 uGUI Button.onClick——
    /// 在 Inspector 里配置背包 / 目标角色 / 属性键，按钮事件绑定 <see cref="Use(string)"/> 并填入道具 ID 即可。
    /// </summary>
    public class ConsumableSkillUser : MonoBehaviour
    {
        [Header("使用参数")]
        [Tooltip("消耗品所在背包（inventoryId，须为已注册数据库中的 Inventory 配置）。")]
        [SerializeField] private string inventoryId = "bag";

        [Tooltip("被施放的目标角色 ID（Chronicle characterId）。")]
        [SerializeField] private string targetCharacterId;

        [Tooltip("道具上承载「使用技能」的属性键（String / String 数组；值为 Chronicle 技能 ID）。")]
        [SerializeField] private string onUseSkillAttrId = "使用技能";

        [Tooltip("是否在 Console 打印每次使用结果。")]
        [SerializeField] private bool logResult = true;

        /// <summary>使用指定消耗品（供 Button.onClick 绑定；在事件里填入道具 ID）。</summary>
        public void Use(string itemId)
        {
            bool ok = ConsumableSkillUse.Use(inventoryId, itemId, targetCharacterId, onUseSkillAttrId);
            if (logResult)
                Debug.Log($"[ConsumableSkillUser] 使用 '{itemId}' → {(ok ? "已施放并扣减" : "未施放（无「使用技能」/ 技能不存在）")}", this);
        }
    }
}
