using System;
using System.Collections.Generic;
using Ale.Toolkit.Runtime;

namespace Ale.Chronicle
{
    /// <summary>
    /// 技能系统运行时管理器（非 MonoBehaviour 单例，首次访问自动创建）。
    ///
    /// <para>职责：
    /// <list type="bullet">
    ///   <item><b>永久已学</b>：按 角色 ID → 已学技能 ID 列表 记录（<see cref="Learn"/> / <see cref="Forget"/>），可存档；</item>
    ///   <item><b>外部来源提供</b>：按 角色 ID → 来源 key → 技能 ID 列表 记录（如装备提供，<see cref="SetProvidedSkills"/>），
    ///         与永久层分开存储、展示合并（<see cref="GetEffectiveSkills"/>），<b>不落盘</b>（读档后由业务层从当前来源重算）；</item>
    ///   <item><b>一次性施放</b>：<see cref="UseSkill"/> 校验技能存在后经 <see cref="OnSkillUsed"/> 派发，
    ///         真正效果由业务层实现（无状态、不落盘）。</item>
    /// </list></para>
    ///
    /// <para>技能目录来自已注册数据库（经 <see cref="ChronicleDataManager"/> 查询）。继承 toolkit 的
    /// <see cref="ToolkitSingleton{T}"/>，关闭 Domain Reload 时也能在每次播放开始复位静态状态（不串数据）。
    /// <b>有效</b>已学集合（永久 ∪ 提供）变化时触发 <see cref="OnLearnedChanged"/> 供技能 UI 刷新。</para>
    /// </summary>
    public class SkillRuntimeManager
        : ToolkitSingleton<SkillRuntimeManager>, ISaveable<RuntimeLearnedSkillState>
    {
        /// <summary>角色 ID → 永久已学技能 ID 列表（保持学习顺序、去重）。按需创建；无已学技能的角色不入字典。</summary>
        private readonly Dictionary<string, List<string>> _learned
            = new Dictionary<string, List<string>>();

        /// <summary>
        /// 角色 ID → 来源 key → 该来源当前提供的技能 ID 列表（保序、去重）。与 <see cref="_learned"/> 分开存储：
        /// 卸下某来源只清该来源、不动永久层；<b>不落盘</b>。按需创建；空来源 / 空角色即从字典移除。
        /// </summary>
        private readonly Dictionary<string, Dictionary<string, List<string>>> _provided
            = new Dictionary<string, Dictionary<string, List<string>>>();

        /// <summary>某角色<b>有效</b>已学技能（永久 ∪ 提供）发生变化时触发。参数为 characterId。供技能 UI 刷新。</summary>
        public event Action<string> OnLearnedChanged;

        /// <summary>技能被<b>使用 / 施放</b>时触发（<see cref="UseSkill"/>）。负载见 <see cref="SkillUseEvent"/>；效果由业务层实现。</summary>
        public event Action<SkillUseEvent> OnSkillUsed;

        protected override void Init()
        {
            // 永久 / 提供 均初始为空，由游戏层学习 / 装备同步 / 存档恢复填充，无需预初始化。
        }

        #region 查询（永久层）

        /// <summary>某角色是否<b>永久</b>学会指定技能（不含外部来源提供；来源合并判断用 <see cref="HasSkill"/>）。</summary>
        public bool HasLearned(string characterId, string skillId)
        {
            return !string.IsNullOrEmpty(characterId) && !string.IsNullOrEmpty(skillId)
                && _learned.TryGetValue(characterId, out var list) && list.Contains(skillId);
        }

        /// <summary>获取某角色的<b>永久</b>已学技能 ID 列表（只读；未学任何技能返回空）。</summary>
        public IReadOnlyList<string> GetLearnedSkillIds(string characterId)
        {
            if (!string.IsNullOrEmpty(characterId) && _learned.TryGetValue(characterId, out var list))
                return list;
            return Array.Empty<string>();
        }

        /// <summary>解析某角色的<b>永久</b>已学技能为技能对象（跳过解析不到的 ID，保持学习顺序）。</summary>
        public List<Skill> GetLearnedSkills(string characterId)
        {
            var result = new List<Skill>();
            var dm     = ChronicleDataManager.Instance;
            if (dm == null || string.IsNullOrEmpty(characterId)) return result;
            if (!_learned.TryGetValue(characterId, out var list)) return result;

            foreach (var id in list)
            {
                var skill = dm.GetSkill(id);
                if (skill != null) result.Add(skill);
            }
            return result;
        }

        #endregion

        #region 学习 / 遗忘（永久层）

        /// <summary>为角色学习一个技能（已学则忽略）。返回是否发生变化。</summary>
        public bool Learn(string characterId, string skillId)
        {
            if (string.IsNullOrEmpty(characterId) || string.IsNullOrEmpty(skillId)) return false;

            if (!_learned.TryGetValue(characterId, out var list))
                _learned[characterId] = list = new List<string>();
            if (list.Contains(skillId)) return false;

            list.Add(skillId);
            OnLearnedChanged?.Invoke(characterId);
            return true;
        }

        /// <summary>让角色遗忘一个技能。返回是否发生变化。</summary>
        public bool Forget(string characterId, string skillId)
        {
            if (string.IsNullOrEmpty(characterId) || string.IsNullOrEmpty(skillId)) return false;
            if (!_learned.TryGetValue(characterId, out var list)) return false;
            if (!list.Remove(skillId)) return false;

            OnLearnedChanged?.Invoke(characterId);
            return true;
        }

        /// <summary>清空某角色的全部<b>永久</b>已学技能（不动外部来源提供层）。</summary>
        public void ClearLearned(string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return;
            if (_learned.Remove(characterId))
                OnLearnedChanged?.Invoke(characterId);
        }

        #endregion

        #region 提供层（装备等外部来源）与合并视图

        /// <summary>
        /// 替换某来源当前提供给角色的<b>全部</b>技能（并集、去重、保序）。内部比对「有效集」变化，
        /// 仅当有效集（永久 ∪ 所有来源）真正变化时才触发 <see cref="OnLearnedChanged"/>。
        /// <paramref name="skillIds"/> 为空 / null 即清空该来源。返回<b>有效集是否变化</b>。
        /// </summary>
        /// <remarks>
        /// 「卸装时若还有其他来源提供该技能则不移除」由本方法的全量替换天然成立：业务层每次传入该来源
        /// 当前完整并集，与仍在的其他来源 / 永久层求并后，共享技能自然留存。
        /// </remarks>
        public bool SetProvidedSkills(string characterId, string providerKey, IEnumerable<string> skillIds)
        {
            if (string.IsNullOrEmpty(characterId) || string.IsNullOrEmpty(providerKey)) return false;

            // 规整新集合：跳过空、去重、保序
            var newList = new List<string>();
            if (skillIds != null)
                foreach (var id in skillIds)
                    if (!string.IsNullOrEmpty(id) && !newList.Contains(id))
                        newList.Add(id);

            _provided.TryGetValue(characterId, out var providers);
            List<string> oldList = null;
            providers?.TryGetValue(providerKey, out oldList);

            // 该来源自身无变化 → 直接返回，不触发
            if (ListEquals(oldList, newList)) return false;

            var before = BuildEffectiveSet(characterId);

            if (newList.Count == 0)
            {
                if (providers != null && providers.Remove(providerKey) && providers.Count == 0)
                    _provided.Remove(characterId);
            }
            else
            {
                if (providers == null) _provided[characterId] = providers = new Dictionary<string, List<string>>();
                providers[providerKey] = newList;
            }

            var after   = BuildEffectiveSet(characterId);
            bool changed = !before.SetEquals(after);
            if (changed) OnLearnedChanged?.Invoke(characterId);
            return changed;
        }

        /// <summary>清空某来源提供给角色的技能（等价于 <see cref="SetProvidedSkills"/> 传空）。返回有效集是否变化。</summary>
        public bool ClearProvider(string characterId, string providerKey)
            => SetProvidedSkills(characterId, providerKey, null);

        /// <summary>清空某角色的<b>全部</b>外部来源提供层（不动永久层）。有效集变化时触发 <see cref="OnLearnedChanged"/>。</summary>
        public void ClearAllProviders(string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return;
            if (!_provided.TryGetValue(characterId, out var providers) || providers.Count == 0) return;

            var before = BuildEffectiveSet(characterId);
            _provided.Remove(characterId);
            var after = BuildEffectiveSet(characterId);   // 仅剩永久层
            if (!before.SetEquals(after)) OnLearnedChanged?.Invoke(characterId);
        }

        /// <summary>某角色是否<b>有效</b>拥有该技能（永久 或 任一来源提供）。</summary>
        public bool HasSkill(string characterId, string skillId)
        {
            if (string.IsNullOrEmpty(characterId) || string.IsNullOrEmpty(skillId)) return false;
            if (_learned.TryGetValue(characterId, out var learned) && learned.Contains(skillId)) return true;
            if (_provided.TryGetValue(characterId, out var providers))
                foreach (var kv in providers)
                    if (kv.Value.Contains(skillId)) return true;
            return false;
        }

        /// <summary>获取某角色<b>有效</b>技能 ID 列表：永久 ∪ 所有来源提供（去重、保序：永久在前，其后按来源插入序）。</summary>
        public IReadOnlyList<string> GetEffectiveSkillIds(string characterId)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(characterId)) return result;

            var seen = new HashSet<string>();
            if (_learned.TryGetValue(characterId, out var learned))
                foreach (var id in learned)
                    if (seen.Add(id)) result.Add(id);
            if (_provided.TryGetValue(characterId, out var providers))
                foreach (var kv in providers)
                    foreach (var id in kv.Value)
                        if (seen.Add(id)) result.Add(id);
            return result;
        }

        /// <summary>解析某角色<b>有效</b>技能为技能对象（永久 ∪ 提供，跳过解析不到的 ID，保序）。技能 UI 采集用此入口。</summary>
        public List<Skill> GetEffectiveSkills(string characterId)
        {
            var result = new List<Skill>();
            var dm     = ChronicleDataManager.Instance;
            if (dm == null || string.IsNullOrEmpty(characterId)) return result;

            foreach (var id in GetEffectiveSkillIds(characterId))
            {
                var skill = dm.GetSkill(id);
                if (skill != null) result.Add(skill);
            }
            return result;
        }

        // 汇总某角色有效技能 ID 集合（永久 ∪ 提供）——用于比对「有效集」是否变化。
        private HashSet<string> BuildEffectiveSet(string characterId)
        {
            var set = new HashSet<string>();
            if (_learned.TryGetValue(characterId, out var learned))
                foreach (var id in learned) set.Add(id);
            if (_provided.TryGetValue(characterId, out var providers))
                foreach (var kv in providers)
                    foreach (var id in kv.Value) set.Add(id);
            return set;
        }

        // 顺序敏感的列表相等（两者均视 null 与空为相等）。因新集合已规整，可直接逐项比对。
        private static bool ListEquals(List<string> a, List<string> b)
        {
            int ca = a?.Count ?? 0, cb = b?.Count ?? 0;
            if (ca != cb) return false;
            for (int i = 0; i < ca; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        #endregion

        #region 使用 / 施放（一次性派发）

        /// <summary>
        /// 对目标角色「使用 / 施放」一个技能：校验技能存在（<see cref="ChronicleDataManager.GetSkill"/> 非空）后，
        /// 经 <see cref="OnSkillUsed"/> 派发 <see cref="SkillUseEvent"/> 并返回 true；技能不存在则返回 false 且不派发。
        /// <para><b>无状态</b>：不改「永久已学」/「提供」层、不触发 <see cref="OnLearnedChanged"/>、不入存档；
        /// 具体效果（治疗 / 加 buff 等）由业务层订阅 <see cref="OnSkillUsed"/> 实现。</para>
        /// </summary>
        /// <param name="targetCharacterId">被施放的目标角色 ID。</param>
        /// <param name="skillId">被施放的技能 ID。</param>
        /// <param name="sourceKey">施放来源标识（可空；如触发本次使用的道具 ID）。</param>
        public bool UseSkill(string targetCharacterId, string skillId, string sourceKey = null)
        {
            if (string.IsNullOrEmpty(targetCharacterId) || string.IsNullOrEmpty(skillId)) return false;
            var dm = ChronicleDataManager.Instance;
            if (dm == null || dm.GetSkill(skillId) == null) return false;

            OnSkillUsed?.Invoke(new SkillUseEvent(targetCharacterId, skillId, sourceKey));
            return true;
        }

        #endregion

        #region 存档

        /// <inheritdoc cref="ISaveable{TState}.GetSaveData"/>
        public List<RuntimeLearnedSkillState> GetSaveData()
        {
            var result = new List<RuntimeLearnedSkillState>(_learned.Count);
            foreach (var kv in _learned)
            {
                var st = new RuntimeLearnedSkillState(kv.Key);
                st.skillIds.AddRange(kv.Value);
                result.Add(st);
            }
            return result;
        }

        /// <inheritdoc cref="ISaveable{TState}.LoadSaveData"/>
        public void LoadSaveData(List<RuntimeLearnedSkillState> data)
        {
            _learned.Clear();
            if (data == null) return;
            foreach (var st in data)
            {
                if (st == null || string.IsNullOrEmpty(st.characterId)) continue;
                var list = new List<string>();
                foreach (var id in st.skillIds)
                    if (!string.IsNullOrEmpty(id) && !list.Contains(id))
                        list.Add(id);
                _learned[st.characterId] = list;
            }
            // 提供层不落盘：加载后由业务层从当前来源（装备等）重算，避免陈旧授予 / 重复计数。
        }

        /// <inheritdoc cref="ISaveable.ResetAll"/>
        public void ResetAll()
        {
            _learned.Clear();
            _provided.Clear();
        }

        #endregion
    }
}
