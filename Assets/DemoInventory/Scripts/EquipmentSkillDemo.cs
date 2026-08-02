using System.Collections.Generic;
using UnityEngine;
using Ale.Inventory.Runtime;   // Inventory* 数据 / 管理器
using Ale.Toolkit.Runtime;     // AttributeValue / AttributeEntry / EFieldType

namespace Ale.Chronicle.Inventory
{
    /// <summary>
    /// 自包含整合 Demo 驱动（Chronicle × Inventory）。<b>拖到空场景里一个 GameObject 上、按 Play 即可验证</b>，
    /// 无需任何数据资产或场景装配：本脚本在运行时用代码构建示例数据（Chronicle 技能 + Inventory 背包 / 道具 / 装备组），
    /// 注册进各管理器，并用 IMGUI 按钮驱动 装备 / 卸下 / 使用，实时显示角色「有效技能」与施放日志。
    ///
    /// <para>覆盖两种模式：① 装备<b>持有</b>技能（<see cref="EquipmentSkillBridge"/> 并集同步，含多来源留存 / 不误删永久）；
    /// ② 使用消耗品<b>触发</b>技能（<see cref="ConsumableSkillUse"/> 施放 + 扣减，效果由 <see cref="OnSkillUsed"/> 日志占位）。</para>
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
        [Tooltip("使用-触发技能 的道具属性键（模式二）。")]
        [SerializeField] private string useAttrId = "使用技能";

        // 示例道具 → 承载的技能（装备用 equipAttrId，消耗品用 useAttrId）。
        private static readonly (string itemId, string label)[] EquipItems =
        {
            ("sword",  "剑 · 技能=火球术"),
            ("amulet", "护符 · 技能=火球术（同技能，验多来源留存）"),
            ("shield", "盾 · 技能=守护"),
        };
        private const string PotionId = "potion";

        private EquipmentSkillBridge _bridge;
        private readonly List<string> _log = new List<string>();
        private GUIStyle _rich;
        private bool _ready;

        private void OnEnable()
        {
            var mgr = SkillRuntimeManager.Instance;
            if (mgr != null) mgr.OnSkillUsed += OnSkillUsed;
        }

        private void OnDisable()
        {
            var mgr = SkillRuntimeManager.Instance;
            if (mgr != null) mgr.OnSkillUsed -= OnSkillUsed;
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
            cdb.Skills.Add(new Skill("fireball"));   // 火球术
            cdb.Skills.Add(new Skill("guard"));      // 守护
            cdb.Skills.Add(new Skill("heal"));       // 治疗（消耗品触发）
            ChronicleDataManager.Instance.Register(cdb);
        }

        private void BuildAndRegisterInventoryData()
        {
            var idb = ScriptableObject.CreateInstance<InventoryDatabase>();

            idb.Inventories.Add(new Ale.Inventory.Runtime.Inventory(inventoryId) { capacity = 0 });   // 0 = 无限背包

            idb.Items.Add(MakeItem("sword",  equipAttrId, "fireball"));
            idb.Items.Add(MakeItem("amulet", equipAttrId, "fireball"));         // 与剑同技能
            idb.Items.Add(MakeItem("shield", equipAttrId, "guard"));
            var potion = MakeItem(PotionId, useAttrId, "heal");
            potion.stackLimit = 0;                                              // 可堆叠
            idb.Items.Add(potion);

            // 装备组：id == characterId；一个宽松槽位列表（无标签 / 无约束）含 3 个空过滤槽 → 任意道具可装。
            var group = new EquipmentGroup(characterId);
            var slotList = new EquipmentSlotList("main");
            slotList.slots.Add(new EquipmentSlot("s1"));
            slotList.slots.Add(new EquipmentSlot("s2"));
            slotList.slots.Add(new EquipmentSlot("s3"));
            group.slotLists.Add(slotList);
            idb.EquipmentGroups.Add(group);

            InventoryDataManager.Instance.Register(idb);
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

            // 先注册 DB（上一步已注册），再 LoadSaveData 创建背包运行时状态（按 capacity 补空槽；本例 0 无需补）。
            InventoryRuntimeManager.Instance.LoadSaveData(
                new List<RuntimeInventoryState> { new RuntimeInventoryState(inventoryId) });

            foreach (var (itemId, _) in EquipItems)
                InventoryRuntimeManager.Instance.TryAddItem(inventoryId, itemId, 1);
            InventoryRuntimeManager.Instance.TryAddItem(inventoryId, PotionId, 3);
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

        // ── 事件 ─────────────────────────────────────────────────────────────

        private void OnSkillUsed(SkillUseEvent e)
        {
            _log.Insert(0, $"施放 '{e.SkillId}' → '{e.TargetCharacterId}'（来源 '{e.SourceKey}'）");
            if (_log.Count > 8) _log.RemoveAt(_log.Count - 1);
        }

        // ── IMGUI ────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (!_ready) return;
            if (_rich == null) _rich = new GUIStyle(GUI.skin.label) { richText = true };

            GUILayout.BeginArea(new Rect(10, 10, 440, Screen.height - 20), GUI.skin.box);

            GUILayout.Label("<b>装备 / 使用 → 技能 整合 Demo</b>", _rich);
            GUILayout.Label($"角色 / 装备组: {characterId}    背包: {inventoryId}");

            GUILayout.Space(6);
            GUILayout.Label("<b>装备（模式一 · 持有）</b>", _rich);
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
            GUILayout.Label("<b>消耗品（模式二 · 触发）</b>", _rich);
            int potionCount = InventoryRuntimeManager.Instance
                ? InventoryRuntimeManager.Instance.GetTotalCount(inventoryId, PotionId) : 0;
            GUILayout.BeginHorizontal();
            GUILayout.Label($"药水 x{potionCount} · 使用技能=治疗");
            GUI.enabled = potionCount > 0;
            if (GUILayout.Button("使用", GUILayout.Width(60)))
                ConsumableSkillUse.Use(inventoryId, PotionId, characterId, useAttrId);
            GUI.enabled = true;
            GUILayout.EndHorizontal();

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
            GUILayout.Label("<b>施放日志（OnSkillUsed 占位效果）</b>", _rich);
            foreach (var line in _log)
                GUILayout.Label("   " + line);

            GUILayout.EndArea();
        }
    }
}
