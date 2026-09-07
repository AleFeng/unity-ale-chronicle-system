using UnityEngine;
using Ale.Inventory.Runtime;   // InventoryRuntimeManager / ItemUseResult

namespace Ale.Chronicle.Inventory
{
    /// <summary>
    /// 使用消耗品「施加效果」（模式二 · 效果契约）——Chronicle × Inventory 整合层（Chronicle 0.4.0 / Inventory 1.12.0）。
    ///
    /// <para>业务流程：道具的 <c>Item.onUseEffectRefs</c> 引用效果 id（可定义在 Chronicle 库，如回复药剂 <c>regen_draught</c>；
    /// 也可定义在 Inventory 库自己的「效果系统」页签）→ <see cref="InventoryRuntimeManager.UseItem"/> 经 toolkit
    /// <c>EffectApplier</c> 把效果按序施加到 <see cref="ChronicleEffectContext.Create"/> 组装的目标上下文
    /// （主体 = 目标角色；定义先经 Chronicle 数据管理器解析，再查全局效果注册表）→ 至少一个效果施加成功才扣减 1 个。</para>
    ///
    /// <para>两个包互不依赖：Inventory 只认 toolkit 的 <c>IEffectContext</c>，Chronicle 负责把效果落到属性 / 特质 / 头衔…；
    /// 本类是唯一同时引用两包的胶水，故放在整合 Demo（<c>Assets/DemoInventory</c>），不进任何运行时包。</para>
    /// </summary>
    public static class ConsumableEffectUse
    {
        /// <summary>
        /// 对 <paramref name="targetCharacterId"/> 使用 <paramref name="inventoryId"/> 中的 <paramref name="itemId"/>；
        /// <paramref name="sourceCharacterId"/>（使用者，可空）进入效果来源与条件「对象」作用域。返回 Inventory 的使用结果。
        /// </summary>
        public static ItemUseResult Use(string inventoryId, string itemId, string targetCharacterId, string sourceCharacterId = null)
        {
            if (string.IsNullOrEmpty(inventoryId) || string.IsNullOrEmpty(itemId) || string.IsNullOrEmpty(targetCharacterId))
                return ItemUseResult.Failed(EItemUseOutcome.InvalidArgument);

            var inv = InventoryRuntimeManager.Instance;   // MonoBehaviour 单例，场景中须存在
            if (inv == null) return ItemUseResult.Failed(EItemUseOutcome.NoContext);

            var ctx = ChronicleEffectContext.Create(targetCharacterId, sourceCharacterId);
            return inv.UseItem(inventoryId, itemId, ctx);
        }
    }

    /// <summary>
    /// <see cref="ConsumableEffectUse"/> 的组件封装，便于把「使用」入口挂到 uGUI Button.onClick——
    /// 在 Inspector 里配置背包 / 目标角色，按钮事件绑定 <see cref="Use(string)"/> 并填入道具 ID 即可。
    /// </summary>
    public class ConsumableEffectUser : MonoBehaviour
    {
        [Header("使用参数")]
        [Tooltip("消耗品所在背包（inventoryId，须为已注册数据库中的 Inventory 配置）。")]
        [SerializeField] private string inventoryId = "bag";

        [Tooltip("效果施加的目标角色 ID（Chronicle characterId）。")]
        [SerializeField] private string targetCharacterId;

        [Tooltip("使用者角色 ID（可空；进入效果来源与条件「对象」作用域）。")]
        [SerializeField] private string sourceCharacterId;

        [Tooltip("是否在 Console 打印每次使用结果。")]
        [SerializeField] private bool logResult = true;

        /// <summary>使用指定消耗品（供 Button.onClick 绑定；在事件里填入道具 ID）。</summary>
        public void Use(string itemId)
        {
            var r = ConsumableEffectUse.Use(inventoryId, itemId, targetCharacterId, sourceCharacterId);
            if (logResult)
                Debug.Log($"[ConsumableEffectUser] 使用 '{itemId}' → {r}", this);
        }
    }
}
