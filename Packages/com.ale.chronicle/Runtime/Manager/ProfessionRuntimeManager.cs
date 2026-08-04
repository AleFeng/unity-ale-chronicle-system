using System;
using System.Collections.Generic;
using Ale.Toolkit.Runtime;

namespace Ale.Chronicle
{
    /// <summary>
    /// 职业系统运行时管理器（非 MonoBehaviour 单例，首次访问自动创建）。按 角色 ID → 职业进度列表 记录，可存档。
    ///
    /// <para>核心：<see cref="AddExp"/> 给某职业加经验，按职业定义的 <see cref="ExpCurve"/> 跑升级循环（<c>maxLevel</c> 封顶），
    /// 每升一级触发 <see cref="OnLevelUp"/> 并施加对应 <see cref="LevelUnlock"/>：授予头衔经 <see cref="TitleRuntimeManager"/>，
    /// 授予特质经 <see cref="OnUnlockTrait"/> 事件（特质授予由业务层实现）。职业目录经 <see cref="ChronicleDataManager"/> 查询。</para>
    ///
    /// <para>继承 <see cref="ToolkitSingleton{T}"/>，关闭 Domain Reload 时也在每次播放开始复位。</para>
    /// </summary>
    public class ProfessionRuntimeManager
        : ToolkitSingleton<ProfessionRuntimeManager>, ISaveable<RuntimeCharacterProfessionState>
    {
        /// <summary>角色 ID → 职业进度列表。按需创建；无职业的角色不入字典。</summary>
        private readonly Dictionary<string, List<ProfessionProgress>> _byChar
            = new Dictionary<string, List<ProfessionProgress>>();

        /// <summary>某角色的职业发生变化（习得 / 放弃 / 升级 / 主职业切换）时触发。参数为 characterId。</summary>
        public event Action<string> OnProfessionsChanged;

        /// <summary>升级时触发（characterId, professionRef, 新等级）。</summary>
        public event Action<string, string, int> OnLevelUp;

        /// <summary>等级解锁授予特质时触发（characterId, traitRef）；特质授予由业务层实现。</summary>
        public event Action<string, string> OnUnlockTrait;

        protected override void Init() { }

        #region 习得 / 放弃 / 主职业

        /// <summary>为角色习得一个职业（1 级）。已从事则忽略。返回是否发生变化。</summary>
        public bool Learn(string characterId, string professionId, bool primary = false)
        {
            if (string.IsNullOrEmpty(characterId) || string.IsNullOrEmpty(professionId)) return false;
            if (Find(characterId, professionId) != null) return false;

            if (!_byChar.TryGetValue(characterId, out var list))
                _byChar[characterId] = list = new List<ProfessionProgress>();
            list.Add(new ProfessionProgress(professionId, 1, 0, primary));
            if (primary) EnforceSinglePrimary(characterId, professionId);
            OnProfessionsChanged?.Invoke(characterId);
            return true;
        }

        /// <summary>让角色放弃一个职业。返回是否发生变化。</summary>
        public bool Abandon(string characterId, string professionId)
        {
            if (string.IsNullOrEmpty(characterId) || string.IsNullOrEmpty(professionId)) return false;
            if (!_byChar.TryGetValue(characterId, out var list)) return false;
            if (list.RemoveAll(p => p != null && p.professionRef == professionId) == 0) return false;
            if (list.Count == 0) _byChar.Remove(characterId);
            OnProfessionsChanged?.Invoke(characterId);
            return true;
        }

        /// <summary>把某职业设为主职业（清除该角色其它职业的主标记）。角色未从事该职业则无操作。</summary>
        public void SetPrimary(string characterId, string professionId)
        {
            if (Find(characterId, professionId) == null) return;
            EnforceSinglePrimary(characterId, professionId);
            OnProfessionsChanged?.Invoke(characterId);
        }

        private void EnforceSinglePrimary(string characterId, string primaryProfessionId)
        {
            if (!_byChar.TryGetValue(characterId, out var list)) return;
            foreach (var p in list)
                if (p != null) p.isPrimary = p.professionRef == primaryProfessionId;
        }

        #endregion

        #region 查询

        /// <summary>某角色是否从事指定职业。</summary>
        public bool HasProfession(string characterId, string professionId) => Find(characterId, professionId) != null;

        /// <summary>某职业当前等级；未从事返回 0。</summary>
        public int GetLevel(string characterId, string professionId) => Find(characterId, professionId)?.level ?? 0;

        /// <summary>某职业当前等级内已积累经验；未从事返回 0。</summary>
        public int GetExp(string characterId, string professionId) => Find(characterId, professionId)?.currentExp ?? 0;

        /// <summary>某职业是否为主职业。</summary>
        public bool IsPrimary(string characterId, string professionId) => Find(characterId, professionId)?.isPrimary ?? false;

        /// <summary>某角色的全部职业进度（只读；无则空）。</summary>
        public IReadOnlyList<ProfessionProgress> GetProfessions(string characterId)
        {
            if (!string.IsNullOrEmpty(characterId) && _byChar.TryGetValue(characterId, out var list))
                return list;
            return Array.Empty<ProfessionProgress>();
        }

        private ProfessionProgress Find(string characterId, string professionId)
        {
            if (string.IsNullOrEmpty(characterId) || string.IsNullOrEmpty(professionId)) return null;
            if (!_byChar.TryGetValue(characterId, out var list)) return null;
            foreach (var p in list)
                if (p != null && p.professionRef == professionId) return p;
            return null;
        }

        #endregion

        #region 加经验 / 升级

        /// <summary>
        /// 给某职业加经验并按 <see cref="ExpCurve"/> 结算升级（<c>maxLevel</c> 封顶，满级丢弃溢出经验）。
        /// 角色未从事该职业则无操作。每升一级触发 <see cref="OnLevelUp"/> 并施加对应 <see cref="LevelUnlock"/>。
        /// </summary>
        public void AddExp(string characterId, string professionId, int amount)
        {
            var pp = Find(characterId, professionId);
            if (pp == null) return;

            pp.currentExp += amount;
            if (pp.currentExp < 0) pp.currentExp = 0;

            var def = ChronicleDataManager.Instance?.GetProfession(professionId);
            if (def != null && def.expCurve != null)
            {
                while (pp.level < def.maxLevel)
                {
                    int need = def.expCurve.ExpToNext(pp.level);
                    if (need <= 0 || pp.currentExp < need) break;   // need<=0 防呆（曲线配置异常时不无限升级）
                    pp.currentExp -= need;
                    pp.level++;
                    OnLevelUp?.Invoke(characterId, professionId, pp.level);
                    ApplyUnlocks(characterId, def, pp.level);
                }
                if (pp.level >= def.maxLevel) pp.currentExp = 0;   // 满级丢弃溢出
            }

            OnProfessionsChanged?.Invoke(characterId);
        }

        private void ApplyUnlocks(string characterId, ProfessionDefinition def, int newLevel)
        {
            if (def.unlocks == null) return;
            foreach (var u in def.unlocks)
            {
                if (u == null || u.level != newLevel) continue;
                if (u.grantTitleRefs != null)
                    foreach (var titleRef in u.grantTitleRefs)
                        if (!string.IsNullOrEmpty(titleRef))
                            TitleRuntimeManager.Instance.Grant(characterId, titleRef, 0);
                if (u.grantTraitRefs != null)
                    foreach (var traitRef in u.grantTraitRefs)
                        if (!string.IsNullOrEmpty(traitRef))
                            OnUnlockTrait?.Invoke(characterId, traitRef);
            }
        }

        #endregion

        #region 存档

        /// <inheritdoc cref="ISaveable{TState}.GetSaveData"/>
        public List<RuntimeCharacterProfessionState> GetSaveData()
        {
            var result = new List<RuntimeCharacterProfessionState>(_byChar.Count);
            foreach (var kv in _byChar)
            {
                var st = new RuntimeCharacterProfessionState(kv.Key);
                foreach (var p in kv.Value) st.professions.Add(p.Clone());
                result.Add(st);
            }
            return result;
        }

        /// <inheritdoc cref="ISaveable{TState}.LoadSaveData"/>
        public void LoadSaveData(List<RuntimeCharacterProfessionState> data)
        {
            _byChar.Clear();
            if (data == null) return;
            foreach (var st in data)
            {
                if (st == null || string.IsNullOrEmpty(st.characterId)) continue;
                var list = new List<ProfessionProgress>();
                if (st.professions != null)
                    foreach (var p in st.professions)
                        if (p != null && !string.IsNullOrEmpty(p.professionRef))
                            list.Add(p.Clone());
                _byChar[st.characterId] = list;
            }
            // 加载不触发事件。
        }

        /// <inheritdoc cref="ISaveable.ResetAll"/>
        public void ResetAll() => _byChar.Clear();

        #endregion
    }
}
