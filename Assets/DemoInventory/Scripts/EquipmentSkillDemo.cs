using System.Collections.Generic;
using UnityEngine;
using Ale.Effect;              // EffectDefinition / EffectMagnitude / EffectModifier / ActiveEffect
using Ale.GameplayTags;        // GameplayTagDefinition
using Ale.Inventory.Runtime;   // Inventory* 数据 / 管理器
using Ale.Modifier;            // EModifierOperation
using Ale.Toolkit.Runtime;     // AttributeValue / AttributeEntry / EFieldType

namespace Ale.Chronicle.Inventory
{
    /// <summary>
    /// 自包含整合 Demo 驱动（Chronicle × Inventory）。<b>拖到空场景里一个 GameObject 上、按 Play 即可验证</b>，
    /// 无需任何数据资产或场景装配：本脚本在运行时用代码构建示例数据（Chronicle 属性 / 角色 / 技能 / 效果 + Inventory 背包 / 道具 / 效果 / 装备组），
    /// 注册进各管理器，并用 IMGUI 按钮驱动 装备 / 卸下 / 使用 / 推进时钟，实时显示角色「有效技能」、耐力 / 战力、活动效果与日志。
    ///
    /// <para>覆盖两种模式：① 装备<b>持有</b>技能（<see cref="EquipmentSkillBridge"/> 并集同步，含多来源留存 / 不误删永久）；
    /// ② 使用消耗品<b>施加效果</b>（<see cref="ConsumableEffectUse"/> → <c>UseItem</c> + <c>ChronicleEffectContext</c>：
    /// 回复药水引用 Chronicle 库定义的 <c>regen_draught</c>，磨刀油引用 Inventory 库自己定义的 <c>sharpen_oil</c>；
    /// 无使用效果的道具（剑）不扣减）。</para>
    ///
    /// <para>仅供开发期验证，非正式游戏 UI；正式整合请用真实数据资产 + uGUI（桥接 / helper 同款）。</para>
    /// </summary>
    public class EquipmentSkillDemo : MonoBehaviour
    {
        [Header("标识")]
        [Tooltip("角色 ID（同时作为装备组 ID）。")]
        [SerializeField] private string characterId = "hero";
        [Tooltip("背包 inventoryId。")]
        [SerializeField] private string inventoryId = "bag";

        [Header("属性键")]
        [Tooltip("装备-授予技能 的道具属性键（模式一）。")]
        [SerializeField] private string equipAttrId = "技能";

        // 示例道具 → 承载的技能（装备用 equipAttrId）。
        private static readonly (string itemId, string label)[] EquipItems =
        {
            ("sword",  "剑 · 技能=火球术"),
            ("amulet", "护符 · 技能=火球术（同技能，验多来源留存）"),
            ("shield", "盾 · 技能=守护"),
        };

        // ── 与效果系统相关的 id ─────────────────────────────────────────────
        private const string PotionId      = "potion";          // 回复药水 → Chronicle 库效果 regen_draught
        private const string OilId         = "oil";             // 磨刀油   → Inventory 库效果 sharpen_oil
        private const string FxRegen       = "regen_draught";   // 定义在 Chronicle 库：5 天、每天 耐力 +5 永久落地
        private const string FxSharpen     = "sharpen_oil";     // 定义在 Inventory 库：3 天 战力 +3
        private const string AtStamina     = "stamina";
        private const string AtMight       = "might";

        private EquipmentSkillBridge _bridge;
        private readonly List<string> _log = new List<string>();
        private GUIStyle _rich;
        private bool _ready;
        private bool _invSubscribed;

        private void OnEnable()
        {
            var mgr = SkillRuntimeManager.Instance;
            if (mgr != null) mgr.OnSkillUsed += OnSkillUsed;
        }

        private void OnDisable()
        {
            var mgr = SkillRuntimeManager.Instance;
            if (mgr != null) mgr.OnSkillUsed -= OnSkillUsed;
            if (_invSubscribed && InventoryRuntimeManager.Instance)
                InventoryRuntimeManager.Instance.OnItemUsed -= OnItemUsed;
            _invSubscribed = false;
        }

        private void Start()
        {
            BuildAndRegisterChronicleData();
            BuildAndRegisterInventoryData();
            SeedInventory();
            SetupBridge();
            _ready = true;
        }

        // ── 数据构建 ─────────────────────────────────────────────────────────

        private void BuildAndRegisterChronicleData()
        {
            var cdb = ScriptableObject.CreateInstance<ChronicleDatabase>();

            // 核心属性 + 角色（效果落到这里）
            cdb.CoreAttributes.Add(new CoreAttributeDefinition(AtStamina) { minValue = 0f, maxValue = 1000f, defaultBase = 10f });
            cdb.CoreAttributes.Add(new CoreAttributeDefinition(AtMight)   { minValue = 0f, maxValue = 100f,  defaultBase = 10f });
            var hero = new CharacterDefinition(characterId);
            hero.coreAttributes.Add(new CoreAttributeValue(AtStamina, 20f));
            hero.coreAttributes.Add(new CoreAttributeValue(AtMight,   12f));
            cdb.Characters.Add(hero);

            // 技能（装备持有 / 触发）
            cdb.Skills.Add(new Skill("fireball"));   // 火球术
            cdb.Skills.Add(new Skill("guard"));      // 守护
            cdb.Skills.Add(new Skill("heal"));       // 治疗

            // 效果：回复药剂（Chronicle 库定义；道具经 onUseEffectRefs 跨库引用）
            var regen = new ChronicleEffect(FxRegen, EDurationPolicy.HasDuration);
            regen.displayText.SetTextValue(0, "回复药剂");
            regen.definition.duration = EffectMagnitude.Scalable(5f);
            regen.definition.period   = EffectMagnitude.Scalable(1f);
            regen.definition.executePeriodicOnApplication = false;
            regen.definition.modifiers.Add(new EffectModifier(AtStamina, EModifierOperation.Add, 5f));
            regen.definition.assetTags.AddTag("Status.Regen");
            regen.Normalize();
            cdb.Effects.Add(regen);

            ChronicleDataManager.Instance.Register(cdb);
        }

        private void BuildAndRegisterInventoryData()
        {
            var idb = ScriptableObject.CreateInstance<InventoryDatabase>();

            idb.Inventories.Add(new Ale.Inventory.Runtime.Inventory(inventoryId) { capacity = 0 });   // 0 = 无限背包

            idb.Items.Add(MakeItem("sword",  equipAttrId, "fireball"));
            idb.Items.Add(MakeItem("amulet", equipAttrId, "fireball"));         // 与剑同技能
            idb.Items.Add(MakeItem("shield", equipAttrId, "guard"));

            // 消耗品：使用时施加效果（stackLimit 0 = 可堆叠）
            var potion = new Item(PotionId) { stackLimit = 0 };
            potion.onUseEffectRefs.Add(FxRegen);      // 跨库：Chronicle 库定义
            idb.Items.Add(potion);
            var oil = new Item(OilId) { stackLimit = 0 };
            oil.onUseEffectRefs.Add(FxSharpen);       // 本库：下方定义
            idb.Items.Add(oil);

            // Inventory 库自己的效果：磨刀（3 天 战力 +3，同目标上限 1 层刷新）
            var sharpen = new EffectDefinition(FxSharpen, EDurationPolicy.HasDuration) { displayName = "磨刀" };
            sharpen.duration     = EffectMagnitude.Scalable(3f);
            sharpen.stackingType = EEffectStackingType.AggregateByTarget;
            sharpen.stackLimit   = 1;
            sharpen.modifiers.Add(new EffectModifier(AtMight, EModifierOperation.Add, 3f));
            sharpen.assetTags.AddTag("Status.Buff.Sharpen");
            sharpen.Normalize();
            idb.Effects.Add(sharpen);
            idb.GameplayTags.Add(new GameplayTagDefinition("Status.Buff.Sharpen", "磨刀类增益"));

            // 装备组：id == characterId；一个宽松槽位列表（无标签 / 无约束）含 3 个空过滤槽 → 任意道具可装。
            var group = new EquipmentGroup(characterId);
            var slotList = new EquipmentSlotList("main");
            slotList.slots.Add(new EquipmentSlot("s1"));
            slotList.slots.Add(new EquipmentSlot("s2"));
            slotList.slots.Add(new EquipmentSlot("s3"));
            group.slotLists.Add(slotList);
            idb.EquipmentGroups.Add(group);

            InventoryDataManager.Instance.Register(idb);   // 同时登记为全局效果定义源 → sharpen_oil 可按 id 解析
        }

        // 直接构建带一个 String 标量属性的道具（值为 Chronicle 技能 ID），无需模板 / RebuildAttributes。
        private static Item MakeItem(string itemId, string attrId, string skillId)
        {
            var item = new Item(itemId);
            var av = new AttributeValue(EFieldType.String);
            av.SetString(0, skillId);                       // 扩容并写入 [0]
            item.values.Add(new AttributeEntry(attrId, av));
            return item;
        }

        private void SeedInventory()
        {
            // InventoryRuntimeManager 是 MonoBehaviour 单例，须场景中存在；无则建一个（其 Awake 立即置 Instance）。
            if (!InventoryRuntimeManager.Instance)
                new GameObject("InventoryRuntime (Demo)").AddComponent<InventoryRuntimeManager>();
            var inv = InventoryRuntimeManager.Instance;
            if (!_invSubscribed) { inv.OnItemUsed += OnItemUsed; _invSubscribed = true; }

            // 先注册 DB（上一步已注册），再 LoadSaveData 创建背包运行时状态（按 capacity 补空槽；本例 0 无需补）。
            inv.LoadSaveData(new List<RuntimeInventoryState> { new RuntimeInventoryState(inventoryId) });

            foreach (var (itemId, _) in EquipItems)
                inv.TryAddItem(inventoryId, itemId, 1);
            inv.TryAddItem(inventoryId, PotionId, 3);
            inv.TryAddItem(inventoryId, OilId, 2);
        }

        private void SetupBridge()
        {
            _bridge = GetComponent<EquipmentSkillBridge>();
            if (!_bridge) _bridge = gameObject.AddComponent<EquipmentSkillBridge>();
            _bridge.Configure(new[] { characterId }, equipAttrId, "equipment");   // groupId == characterId
        }

        // ── 装备操作（模式一）────────────────────────────────────────────────

        private void EquipItem(string itemId)
            => EquipmentRuntimeManager.Instance.TryAutoEquip(characterId, itemId, inventoryId);

        private void UnequipItem(string itemId)
        {
            var eq = EquipmentRuntimeManager.Instance;
            var group = InventoryDataManager.Instance?.GetEquipmentGroup(characterId);
            if (eq == null || group == null) return;
            foreach (var sl in group.slotLists)
                foreach (var slot in sl.slots)
                    if (eq.GetEquipped(characterId, slot.id) == itemId)
                    {
                        eq.Unequip(characterId, slot.id, inventoryId);
                        return;
                    }
        }

        private bool IsEquipped(string itemId)
        {
            var eq = EquipmentRuntimeManager.Instance;
            var group = InventoryDataManager.Instance?.GetEquipmentGroup(characterId);
            if (eq == null || group == null) return false;
            foreach (var sl in group.slotLists)
                foreach (var slot in sl.slots)
                    if (eq.GetEquipped(characterId, slot.id) == itemId) return true;
            return false;
        }

        // ── 使用 / 时钟（模式二；公开供按钮 / 自动化验证调用）──────────────────

        /// <summary>使用一瓶回复药水（效果定义在 Chronicle 库）。</summary>
        public ItemUseResult UsePotion() => ConsumableEffectUse.Use(inventoryId, PotionId, characterId, characterId);

        /// <summary>使用一份磨刀油（效果定义在 Inventory 库，经全局效果注册表解析）。</summary>
        public ItemUseResult UseOil() => ConsumableEffectUse.Use(inventoryId, OilId, characterId, characterId);

        /// <summary>「使用」剑：无使用效果 → NoEffects，不扣减。</summary>
        public ItemUseResult UseSword() => ConsumableEffectUse.Use(inventoryId, "sword", characterId, characterId);

        /// <summary>推进世界时钟（周期结算 / 到期）。</summary>
        public void AdvanceDays(int days) => ChronicleClock.Instance.AdvanceDays(days);

        public int   PotionCount => InventoryRuntimeManager.Instance ? InventoryRuntimeManager.Instance.GetTotalCount(inventoryId, PotionId) : 0;
        public int   OilCount    => InventoryRuntimeManager.Instance ? InventoryRuntimeManager.Instance.GetTotalCount(inventoryId, OilId) : 0;
        public float Stamina     => ChronicleCharacterRuntime.EvaluateValue(characterId, AtStamina);
        public float Might       => ChronicleCharacterRuntime.EvaluateValue(characterId, AtMight);
        public IReadOnlyList<ActiveEffect> ActiveEffects => EffectRuntimeManager.Instance.GetActiveEffects(characterId);

        // ── 事件 ─────────────────────────────────────────────────────────────

        private void OnSkillUsed(SkillUseEvent e)
            => Log($"施放 '{e.SkillId}' → '{e.TargetCharacterId}'（来源 '{e.SourceKey}'，{e.EffectsApplied} 个效果生效）");

        private void OnItemUsed(ItemUseEvent e)
            => Log($"使用 '{e.ItemId}' → {Outcome(e.Result.Outcome)}，扣减={e.Result.Consumed}，效果 {e.Result.AppliedCount}/{e.Result.EffectResults.Count}");

        private void Log(string line)
        {
            _log.Insert(0, line);
            if (_log.Count > 8) _log.RemoveAt(_log.Count - 1);
        }

        private static string Outcome(EItemUseOutcome o)
        {
            switch (o)
            {
                case EItemUseOutcome.Used:      return "已使用";
                case EItemUseOutcome.Blocked:   return "全部被阻断";
                case EItemUseOutcome.NoEffects: return "无使用效果（不扣减）";
                case EItemUseOutcome.NotOwned:  return "未持有";
                default:                        return o.ToString();
            }
        }

        // ── IMGUI ────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (!_ready) return;
            if (_rich == null) _rich = new GUIStyle(GUI.skin.label) { richText = true };

            GUILayout.BeginArea(new Rect(10, 10, 480, Screen.height - 20), GUI.skin.box);

            GUILayout.Label("<b>装备 / 使用 → 技能 · 效果 整合 Demo</b>", _rich);
            GUILayout.Label($"角色 / 装备组: {characterId}    背包: {inventoryId}    第 {ChronicleClock.Instance.WorldDay} 天");

            GUILayout.Space(6);
            GUILayout.Label("<b>装备（模式一 · 持有技能）</b>", _rich);
            foreach (var (itemId, label) in EquipItems)
            {
                bool equipped = IsEquipped(itemId);
                GUILayout.BeginHorizontal();
                GUILayout.Label((equipped ? "[已装备] " : "[背包]   ") + label);
                if (GUILayout.Button(equipped ? "卸下" : "装备", GUILayout.Width(60)))
                {
                    if (equipped) UnequipItem(itemId); else EquipItem(itemId);
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(6);
            GUILayout.Label("<b>消耗品（模式二 · 使用施加效果）</b>", _rich);
            DrawUseRow($"回复药水 x{PotionCount} · 效果=回复药剂（Chronicle 库定义：5 天每天耐力 +5 永久）", PotionCount > 0, () => UsePotion());
            DrawUseRow($"磨刀油 x{OilCount} · 效果=磨刀（Inventory 库定义：3 天战力 +3）", OilCount > 0, () => UseOil());
            DrawUseRow("剑（无使用效果 → 不扣减）", !IsEquipped("sword"), () => UseSword());

            GUILayout.BeginHorizontal();
            GUILayout.Label("世界时钟");
            if (GUILayout.Button("推进 1 天", GUILayout.Width(80))) AdvanceDays(1);
            if (GUILayout.Button("推进 5 天", GUILayout.Width(80))) AdvanceDays(5);
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label($"<b>角色状态</b>   耐力 <b>{Stamina:0.##}</b>   战力 <b>{Might:0.##}</b>", _rich);
            var active = ActiveEffects;
            if (active.Count == 0) GUILayout.Label("   （无活动效果）");
            foreach (var e in active)
            {
                string name = e.Definition != null ? (string.IsNullOrEmpty(e.Definition.displayName) ? e.Definition.id : e.Definition.displayName) : "?";
                string rem  = e.HasDuration ? $"剩余 {Mathf.CeilToInt(e.Remaining)} 天" : "永久";
                GUILayout.Label($"   • {name}  [{rem}{(e.IsPeriodic ? " · 每天结算" : "")}]");
            }

            GUILayout.Space(6);
            GUILayout.Label("<b>永久技能（验不误删）</b>", _rich);
            bool perm = SkillRuntimeManager.Instance.HasLearned(characterId, "fireball");
            if (GUILayout.Button(perm ? "Forget 火球术（永久）" : "Learn 火球术（永久）", GUILayout.Width(200)))
            {
                if (perm) SkillRuntimeManager.Instance.Forget(characterId, "fireball");
                else      SkillRuntimeManager.Instance.Learn(characterId, "fireball");
            }

            GUILayout.Space(6);
            GUILayout.Label("<b>有效技能（永久 ∪ 装备提供）</b>", _rich);
            foreach (var id in SkillRuntimeManager.Instance.GetEffectiveSkillIds(characterId))
                GUILayout.Label("   • " + id);

            GUILayout.Space(6);
            GUILayout.Label("<b>日志（OnSkillUsed / OnItemUsed）</b>", _rich);
            foreach (var line in _log)
                GUILayout.Label("   " + line);

            GUILayout.EndArea();
        }

        private static void DrawUseRow(string label, bool enabled, System.Action use)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label);
            GUI.enabled = enabled;
            if (GUILayout.Button("使用", GUILayout.Width(60))) use();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }
    }
}
