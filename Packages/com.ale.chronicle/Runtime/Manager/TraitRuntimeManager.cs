using System;
using System.Collections.Generic;
using Ale.Modifier;
using Ale.Toolkit.Runtime;

namespace Ale.Chronicle
{
    /// <summary>
    /// 运行时特质管理器：管理角色<b>运行期授予</b>的特质实例（效果执行器 / 职业解锁 / 游戏逻辑），与配置层
    /// <see cref="CharacterDefinition.traits"/>（永久、随角色定义）合成「有效特质」；临时特质随 <see cref="ChronicleClock"/>
    /// 推进到期自动移除。同 <see cref="SkillRuntimeManager"/> 形态：单例 + 存档。
    ///
    /// <para><b>授予规则</b>：特质须存在于 <see cref="ChronicleDataManager"/>；配置层已持有 → 不重复授予（返回 false）；
    /// 与任一有效特质硬互斥（<see cref="TraitDefinition.IsIncompatibleWith"/>：同等价组 / 互斥列表）→ 拒绝；
    /// 运行时已持有 → 合并：层数累加，临时特质按 <see cref="TraitDefinition.durationStacksRefresh"/> 刷新剩余时长（取较大值）或保持。</para>
    ///
    /// <para><b>时长约定</b>（<see cref="Grant"/> 的 durationDays）：0 = 按特质定义（临时 → <see cref="TraitDefinition.defaultDurationDays"/>，永久 → 永久）；
    /// &gt;0 = 指定天数；&lt;0 = 强制永久。与执行器 <c>Chronicle.GrantTrait</c> 的参数一致。</para>
    /// </summary>
    public class TraitRuntimeManager : ToolkitSingleton<TraitRuntimeManager>, ISaveable<RuntimeCharacterTraitState>
    {
        private readonly Dictionary<string, List<CharacterTraitInstance>> _byChar
            = new Dictionary<string, List<CharacterTraitInstance>>();

        /// <summary>某角色的运行时特质集合变化（授予 / 移除 / 到期 / 合并）。</summary>
        public event Action<string> OnTraitsChanged;

        /// <summary>新授予运行时特质：(角色 id, 特质 id)。合并到既有实例不触发。</summary>
        public event Action<string, string> OnTraitGranted;

        /// <summary>运行时特质被移除（手动 / 按来源 / 到期）：(角色 id, 特质 id)。</summary>
        public event Action<string, string> OnTraitRevoked;

        protected override void Init() { }

        #region 授予 / 移除

        /// <summary>
        /// 授予运行时特质。<paramref name="durationDays"/>：0 按特质定义、&gt;0 指定天数、&lt;0 强制永久；
        /// <paramref name="stacks"/> &lt; 1 视为 1；<paramref name="sourceTag"/> 供 <see cref="RevokeBySource"/> 成组撤销（如 <c>effect:{id}</c>）。
        /// 返回是否授予或合并成功（特质不存在 / 配置层已持有 / 互斥 / 临时特质无有效时长 → false）。
        /// </summary>
        public bool Grant(string characterId, string traitId, float durationDays = 0f, int stacks = 1, string sourceTag = null)
        {
            if (string.IsNullOrEmpty(characterId) || string.IsNullOrEmpty(traitId)) return false;
            var dm  = ChronicleDataManager.Instance;
            var def = dm?.GetTrait(traitId);
            if (def == null) return false;
            if (stacks < 1) stacks = 1;

            // 解析剩余时长（-1 = 永久）
            float remaining;
            if (durationDays > 0f)      remaining = durationDays;
            else if (durationDays < 0f) remaining = -1f;
            else                        remaining = def.IsTemporary ? def.defaultDurationDays : -1f;
            if (remaining >= 0f && remaining <= 0f) return false;   // 临时特质却无有效时长：配置缺失，不授予

            // 配置层已持有（永久）→ 不重复
            if (HasInConfig(characterId, traitId, dm)) return false;

            var list = GetOrCreate(characterId);
            int idx  = IndexOf(list, traitId);
            if (idx >= 0)
            {
                // 合并：层数累加；永久授予升级为永久；临时按定义刷新（取较大剩余）或保持
                var cur = list[idx];
                cur.stacks += stacks;
                if (remaining < 0f)                                            cur.remainingDays = -1f;
                else if (!cur.IsPermanent && def.durationStacksRefresh
                         && remaining > cur.remainingDays)                     cur.remainingDays = remaining;
                if (string.IsNullOrEmpty(cur.sourceTag) && !string.IsNullOrEmpty(sourceTag)) cur.sourceTag = sourceTag;
                list[idx] = cur;
                OnTraitsChanged?.Invoke(characterId);
                return true;
            }

            // 互斥：与任一有效特质（配置 ∪ 运行时）硬互斥 → 拒绝
            foreach (var existing in GetEffectiveTraits(characterId))
            {
                var other = dm.GetTrait(existing.traitRef);
                if (other != null && other.IsIncompatibleWith(def)) return false;
            }

            list.Add(new CharacterTraitInstance(traitId, remaining, stacks, sourceTag));
            OnTraitGranted?.Invoke(characterId, traitId);
            OnTraitsChanged?.Invoke(characterId);
            return true;
        }

        /// <summary>移除运行时特质（配置层特质不受影响）。</summary>
        public bool Revoke(string characterId, string traitId)
        {
            if (string.IsNullOrEmpty(characterId) || string.IsNullOrEmpty(traitId)) return false;
            if (!_byChar.TryGetValue(characterId, out var list)) return false;
            int idx = IndexOf(list, traitId);
            if (idx < 0) return false;

            list.RemoveAt(idx);
            if (list.Count == 0) _byChar.Remove(characterId);
            OnTraitRevoked?.Invoke(characterId, traitId);
            OnTraitsChanged?.Invoke(characterId);
            return true;
        }

        /// <summary>按来源标记成组移除运行时特质，返回移除数。</summary>
        public int RevokeBySource(string characterId, string sourceTag)
        {
            if (string.IsNullOrEmpty(characterId) || string.IsNullOrEmpty(sourceTag)) return 0;
            if (!_byChar.TryGetValue(characterId, out var list)) return 0;

            var removed = new List<string>();
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].sourceTag != sourceTag) continue;
                removed.Add(list[i].traitRef);
                list.RemoveAt(i);
            }
            if (removed.Count == 0) return 0;
            if (list.Count == 0) _byChar.Remove(characterId);

            for (int i = removed.Count - 1; i >= 0; i--) OnTraitRevoked?.Invoke(characterId, removed[i]);
            OnTraitsChanged?.Invoke(characterId);
            return removed.Count;
        }

        /// <summary>清空某角色的全部运行时特质。</summary>
        public void ClearRuntime(string characterId)
        {
            if (string.IsNullOrEmpty(characterId) || !_byChar.TryGetValue(characterId, out var list)) return;
            var ids = new List<string>();
            foreach (var t in list) ids.Add(t.traitRef);
            _byChar.Remove(characterId);
            foreach (var id in ids) OnTraitRevoked?.Invoke(characterId, id);
            OnTraitsChanged?.Invoke(characterId);
        }

        #endregion

        #region 查询

        /// <summary>是否持有（配置层 ∪ 运行时）。</summary>
        public bool Has(string characterId, string traitId)
            => HasRuntime(characterId, traitId) || HasInConfig(characterId, traitId, ChronicleDataManager.Instance);

        /// <summary>是否持有运行时实例。</summary>
        public bool HasRuntime(string characterId, string traitId)
            => !string.IsNullOrEmpty(characterId) && !string.IsNullOrEmpty(traitId)
               && _byChar.TryGetValue(characterId, out var list) && IndexOf(list, traitId) >= 0;

        /// <summary>取运行时实例（剩余天数 / 层数 / 来源）。</summary>
        public bool TryGetRuntime(string characterId, string traitId, out CharacterTraitInstance instance)
        {
            instance = default;
            if (string.IsNullOrEmpty(characterId) || string.IsNullOrEmpty(traitId)) return false;
            if (!_byChar.TryGetValue(characterId, out var list)) return false;
            int idx = IndexOf(list, traitId);
            if (idx < 0) return false;
            instance = list[idx];
            return true;
        }

        /// <summary>某角色的运行时特质实例（只读；未持有返回空）。</summary>
        public IReadOnlyList<CharacterTraitInstance> GetRuntimeTraits(string characterId)
        {
            if (!string.IsNullOrEmpty(characterId) && _byChar.TryGetValue(characterId, out var list))
                return list;
            return Array.Empty<CharacterTraitInstance>();
        }

        /// <summary>
        /// 有效特质 = 配置层（<paramref name="configCharacter"/> 或经数据管理器按 id 反查）∪ 运行时；按 traitRef 去重，配置层优先。
        /// </summary>
        public List<CharacterTraitInstance> GetEffectiveTraits(string characterId, CharacterDefinition configCharacter = null)
        {
            var result = new List<CharacterTraitInstance>();
            var seen   = new HashSet<string>();

            var character = configCharacter ?? ChronicleDataManager.Instance?.GetCharacter(characterId);
            if (character?.traits != null)
                foreach (var ti in character.traits)
                    if (!string.IsNullOrEmpty(ti.traitRef) && seen.Add(ti.traitRef)) result.Add(ti);

            if (!string.IsNullOrEmpty(characterId) && _byChar.TryGetValue(characterId, out var list))
                foreach (var ti in list)
                    if (!string.IsNullOrEmpty(ti.traitRef) && seen.Add(ti.traitRef)) result.Add(ti);

            return result;
        }

        /// <summary>
        /// 把运行时特质作用于 <paramref name="targetAttributeId"/> 的修饰器（克隆、来源 <c>trait:{id}</c>）追加进 <paramref name="into"/>；
        /// 配置层已持有的同名特质跳过（由配置层收集）。层数不参与幅度换算（与配置层一致）。传空属性 id 收集全部。
        /// </summary>
        public void CollectModifiers(string characterId, string targetAttributeId, List<ModifierDefinition> into)
        {
            if (into == null || string.IsNullOrEmpty(characterId)) return;
            if (!_byChar.TryGetValue(characterId, out var list)) return;
            var dm = ChronicleDataManager.Instance;
            if (dm == null) return;

            foreach (var ti in list)
            {
                if (HasInConfig(characterId, ti.traitRef, dm)) continue;
                dm.GetTrait(ti.traitRef)?.CollectModifiers(targetAttributeId, into);
            }
        }

        #endregion

        #region 推进

        /// <summary>推进 <paramref name="days"/> 天：临时特质剩余递减，到期移除并派发事件。</summary>
        public void Tick(float days)
        {
            if (days <= 0f || _byChar.Count == 0) return;

            var expired = new List<(string characterId, string traitId)>();
            var changed = new List<string>();
            var ids     = new List<string>(_byChar.Keys);
            foreach (var characterId in ids)
            {
                if (!_byChar.TryGetValue(characterId, out var list)) continue;
                bool any = false;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var t = list[i];
                    if (t.IsPermanent) continue;
                    t = t.Ticked(days);
                    any = true;
                    if (t.IsExpired)
                    {
                        list.RemoveAt(i);
                        expired.Add((characterId, t.traitRef));
                    }
                    else list[i] = t;
                }
                if (list.Count == 0) _byChar.Remove(characterId);
                if (any) changed.Add(characterId);
            }

            // 先按顺序派发到期，再派发集合变化（逆序移除 → 还原为授予顺序）
            for (int i = expired.Count - 1; i >= 0; i--)
                OnTraitRevoked?.Invoke(expired[i].characterId, expired[i].traitId);
            foreach (var characterId in changed)
                OnTraitsChanged?.Invoke(characterId);
        }

        #endregion

        #region 存档

        public List<RuntimeCharacterTraitState> GetSaveData()
        {
            var result = new List<RuntimeCharacterTraitState>(_byChar.Count);
            foreach (var kv in _byChar)
            {
                var st = new RuntimeCharacterTraitState(kv.Key);
                st.traits.AddRange(kv.Value);
                result.Add(st);
            }
            return result;
        }

        public void LoadSaveData(List<RuntimeCharacterTraitState> data)
        {
            _byChar.Clear();
            if (data == null) return;
            foreach (var st in data)
            {
                if (st == null || string.IsNullOrEmpty(st.characterId) || st.traits == null) continue;
                var list = new List<CharacterTraitInstance>();
                foreach (var t in st.traits)
                    if (!string.IsNullOrEmpty(t.traitRef) && IndexOf(list, t.traitRef) < 0)
                        list.Add(t);
                if (list.Count > 0) _byChar[st.characterId] = list;
            }
            // 加载不触发事件。
        }

        public void ResetAll() => _byChar.Clear();

        #endregion

        #region 内部

        private List<CharacterTraitInstance> GetOrCreate(string characterId)
        {
            if (!_byChar.TryGetValue(characterId, out var list))
                _byChar[characterId] = list = new List<CharacterTraitInstance>();
            return list;
        }

        private static int IndexOf(List<CharacterTraitInstance> list, string traitId)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i].traitRef == traitId) return i;
            return -1;
        }

        private static bool HasInConfig(string characterId, string traitId, ChronicleDataManager dm)
        {
            var character = dm?.GetCharacter(characterId);
            if (character?.traits == null) return false;
            foreach (var ti in character.traits)
                if (ti.traitRef == traitId) return true;
            return false;
        }

        #endregion
    }
}
