using System;
using System.Collections.Generic;
using UnityEngine;
using Ale.Inventory.Runtime;   // EquipmentRuntimeManager / InventoryDataManager / EquipmentGroup ...
using Ale.Toolkit.Runtime;     // EFieldType

namespace Ale.Chronicle.Inventory
{
    /// <summary>
    /// 装备「持有」技能桥（模式一 · Approach P）——Chronicle × Inventory 整合层。
    ///
    /// <para>订阅 Inventory 的 <see cref="EquipmentRuntimeManager.OnEquipmentChanged"/>（参数为装备组 ID），
    /// 遍历该组当前所有已装备道具、求其「装备-授予技能」属性（默认键 <c>"技能"</c>，String / String 数组，
    /// 值为 <b>Chronicle 技能 ID</b>）的并集，作为一个来源整体同步到 Chronicle 的
    /// <see cref="SkillRuntimeManager.SetProvidedSkills"/>。</para>
    ///
    /// <para>「卸装时若还有其他装备提供该技能则不移除」「不误删角色永久学会的技能」由 Chronicle 提供层的
    /// 全量并集替换天然成立——本桥只负责重算并集与组↔角色映射，不含引用计数。</para>
    ///
    /// <para>这是<b>唯一</b>同时引用 Inventory 与 Chronicle 两包的整合代码，故放在整合 Demo
    /// （<c>Assets/DemoInventory</c>），不进任何运行时包，保持两包互不依赖。</para>
    /// </summary>
    public class EquipmentSkillBridge : MonoBehaviour
    {
        /// <summary>装备组 ID ≠ 角色 ID 时的映射项。</summary>
        [Serializable]
        public struct GroupCharacterPair
        {
            [Tooltip("Inventory 装备组 ID")] public string groupId;
            [Tooltip("Chronicle 角色 ID")]   public string characterId;
        }

        [Header("同步范围")]
        [Tooltip("需要同步的装备组 ID 列表（Start / 读档后全量重算）。默认 groupId 即 characterId。")]
        [SerializeField] private List<string> groupIds = new List<string>();

        [Tooltip("装备组 ID 与角色 ID 不一致时的映射覆盖；未列出的组按 groupId == characterId。")]
        [SerializeField] private List<GroupCharacterPair> overrides = new List<GroupCharacterPair>();

        [Header("配置键")]
        [Tooltip("道具上承载「装备-授予技能」的属性键（String / String 数组；值为 Chronicle 技能 ID）。")]
        [SerializeField] private string equipSkillAttrId = "技能";

        [Tooltip("写入 Chronicle 提供层时使用的来源 key（每角色一个装备来源槽）。")]
        [SerializeField] private string providerKey = "equipment";

        private void OnEnable()
        {
            var eq = EquipmentRuntimeManager.Instance;
            if (eq != null) eq.OnEquipmentChanged += Resync;
        }

        private void OnDisable()
        {
            var eq = EquipmentRuntimeManager.Instance;
            if (eq != null) eq.OnEquipmentChanged -= Resync;
        }

        private void Start() => ResyncAll();

        /// <summary>重算并同步所有配置的装备组（供 Start / 读档恢复装备后调用）。</summary>
        public void ResyncAll()
        {
            foreach (var groupId in groupIds)
                Resync(groupId);
        }

        /// <summary>重算某装备组当前已装备道具授予的技能并集，整体同步到对应角色的 Chronicle 提供层。</summary>
        public void Resync(string groupId)
        {
            if (string.IsNullOrEmpty(groupId)) return;
            var skillMgr = SkillRuntimeManager.Instance;
            if (skillMgr == null) return;

            skillMgr.SetProvidedSkills(MapToCharacter(groupId), providerKey, CollectEquippedSkillIds(groupId));
        }

        // 遍历该装备组所有槽位的已装备道具，收集其 equipSkillAttrId 属性中的技能 ID（并集、去重、保序）。
        private List<string> CollectEquippedSkillIds(string groupId)
        {
            var result = new List<string>();
            var seen   = new HashSet<string>();

            var dm = InventoryDataManager.Instance;
            var eq = EquipmentRuntimeManager.Instance;
            if (dm == null || eq == null) return result;

            var group = dm.GetEquipmentGroup(groupId);
            if (group == null) return result;

            foreach (var slotList in group.slotLists)
            {
                if (slotList == null) continue;
                foreach (var slot in slotList.slots)
                {
                    if (slot == null) continue;
                    var itemId = eq.GetEquipped(groupId, slot.id);
                    if (string.IsNullOrEmpty(itemId)) continue;   // 空槽，跳过（不把 null 传给 GetItem）

                    var av = dm.GetItem(itemId)?.GetAttributeValue(equipSkillAttrId);
                    if (av == null || av.Type != EFieldType.String) continue;

                    var ids = av.StringArray;
                    if (ids == null) continue;
                    foreach (var id in ids)
                        if (!string.IsNullOrEmpty(id) && seen.Add(id))
                            result.Add(id);
                }
            }
            return result;
        }

        // 组 → 角色映射：命中 overrides 则用其 characterId（为空回退 groupId），否则恒等。
        private string MapToCharacter(string groupId)
        {
            foreach (var pair in overrides)
                if (pair.groupId == groupId)
                    return string.IsNullOrEmpty(pair.characterId) ? groupId : pair.characterId;
            return groupId;
        }
    }
}
