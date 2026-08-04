using System;
using System.Collections.Generic;
using Ale.Toolkit.Runtime;

namespace Ale.Chronicle
{
    /// <summary>
    /// 头衔系统运行时管理器（非 MonoBehaviour 单例，首次访问自动创建）。按 角色 ID → 持有头衔列表 记录，可存档。
    ///
    /// <para>授予规则（<see cref="Grant"/>）：
    /// <list type="bullet">
    ///   <item><b>阶级头衔（RankTitle）晋升替换</b>：授予某阶梯内的头衔时，移除该角色在<b>同一阶梯</b>已持的其它头衔——「同时只持其一」；</item>
    ///   <item><b>唯一头衔</b>（<see cref="TitleDefinition.isUnique"/>）：从任何其他持有者剥夺并触发 <see cref="OnTitleTransferred"/>。</item>
    /// </list>
    /// 头衔目录经 <see cref="ChronicleDataManager"/> 查询（含 <see cref="ChronicleDataManager.GetAllRankLadders"/> 反查阶梯归属）。
    /// 继承 <see cref="ToolkitSingleton{T}"/>，关闭 Domain Reload 时也在每次播放开始复位。</para>
    /// </summary>
    public class TitleRuntimeManager
        : ToolkitSingleton<TitleRuntimeManager>, ISaveable<RuntimeCharacterTitleState>
    {
        /// <summary>角色 ID → 持有头衔列表（按获得顺序）。按需创建；无头衔的角色不入字典。</summary>
        private readonly Dictionary<string, List<TitleHolding>> _byChar
            = new Dictionary<string, List<TitleHolding>>();

        /// <summary>某角色持有头衔发生变化时触发。参数为 characterId。</summary>
        public event Action<string> OnTitlesChanged;

        /// <summary>唯一头衔易主时触发（titleId, 原持有者 charId, 新持有者 charId）。</summary>
        public event Action<string, string, string> OnTitleTransferred;

        protected override void Init() { }

        /// <summary>
        /// 授予头衔。已持有则幂等返回。阶级头衔按阶梯「晋升替换」、唯一头衔从他人剥夺（见类注释）。
        /// </summary>
        public void Grant(string characterId, string titleRef, int worldDay = 0)
        {
            if (string.IsNullOrEmpty(characterId) || string.IsNullOrEmpty(titleRef)) return;
            if (Has(characterId, titleRef)) return;   // 幂等

            var dm  = ChronicleDataManager.Instance;
            var def = dm != null ? dm.GetTitle(titleRef) : null;

            // ① 阶级头衔：晋升替换——移除该角色在同一阶梯已持的其它头衔
            if (def != null && def.kind == ETitleKind.RankTitle && dm != null)
            {
                var ladder = FindLadderContaining(dm, titleRef);
                if (ladder != null && _byChar.TryGetValue(characterId, out var own))
                    own.RemoveAll(h => h != null && ladder.orderedTitleRefs.Contains(h.titleRef));
            }

            // ② 唯一头衔：从任何其他持有者剥夺
            if (def != null && def.isUnique)
            {
                string prev = FindOtherHolder(titleRef, characterId);
                if (!string.IsNullOrEmpty(prev))
                {
                    RemoveTitleInternal(prev, titleRef);
                    OnTitleTransferred?.Invoke(titleRef, prev, characterId);
                    OnTitlesChanged?.Invoke(prev);
                }
            }

            // ③ 授予
            if (!_byChar.TryGetValue(characterId, out var list))
                _byChar[characterId] = list = new List<TitleHolding>();
            list.Add(new TitleHolding(titleRef, worldDay));
            OnTitlesChanged?.Invoke(characterId);
        }

        /// <summary>剥夺头衔。若头衔定义存在且 <see cref="TitleDefinition.isRevocable"/> 为 false 则拒绝（返回 false）。返回是否发生变化。</summary>
        public bool Revoke(string characterId, string titleRef)
        {
            if (string.IsNullOrEmpty(characterId) || string.IsNullOrEmpty(titleRef)) return false;
            var def = ChronicleDataManager.Instance?.GetTitle(titleRef);
            if (def != null && !def.isRevocable) return false;

            if (!_byChar.TryGetValue(characterId, out var list)) return false;
            if (list.RemoveAll(h => h != null && h.titleRef == titleRef) == 0) return false;
            if (list.Count == 0) _byChar.Remove(characterId);
            OnTitlesChanged?.Invoke(characterId);
            return true;
        }

        /// <summary>某角色是否持有指定头衔。</summary>
        public bool Has(string characterId, string titleRef)
            => !string.IsNullOrEmpty(characterId) && !string.IsNullOrEmpty(titleRef)
               && _byChar.TryGetValue(characterId, out var list)
               && list.Exists(h => h != null && h.titleRef == titleRef);

        /// <summary>某角色持有的头衔列表（只读；无则空）。</summary>
        public IReadOnlyList<TitleHolding> GetTitles(string characterId)
        {
            if (!string.IsNullOrEmpty(characterId) && _byChar.TryGetValue(characterId, out var list))
                return list;
            return Array.Empty<TitleHolding>();
        }

        /// <summary>某角色在指定阶级序列上的最高位阶（rankTier）；未持该序列任何头衔返回 <see cref="int.MinValue"/>。</summary>
        public int GetHighestRankTier(string characterId, string ladderId)
        {
            var dm = ChronicleDataManager.Instance;
            if (dm == null || string.IsNullOrEmpty(characterId) || string.IsNullOrEmpty(ladderId)) return int.MinValue;
            var ladder = dm.GetRankLadder(ladderId);
            if (ladder == null || !_byChar.TryGetValue(characterId, out var list)) return int.MinValue;

            int best = int.MinValue;
            foreach (var h in list)
            {
                if (h == null || !ladder.orderedTitleRefs.Contains(h.titleRef)) continue;
                var title = dm.GetTitle(h.titleRef);
                if (title != null && title.rankTier > best) best = title.rankTier;
            }
            return best;
        }

        // ── 内部辅助 ────────────────────────────────────────────────────────────────

        private static RankLadder FindLadderContaining(ChronicleDataManager dm, string titleRef)
        {
            foreach (var l in dm.GetAllRankLadders())
                if (l != null && l.orderedTitleRefs != null && l.orderedTitleRefs.Contains(titleRef))
                    return l;
            return null;
        }

        private string FindOtherHolder(string titleRef, string exceptCharId)
        {
            foreach (var kv in _byChar)
            {
                if (kv.Key == exceptCharId) continue;
                if (kv.Value.Exists(h => h != null && h.titleRef == titleRef)) return kv.Key;
            }
            return null;
        }

        private void RemoveTitleInternal(string characterId, string titleRef)
        {
            if (!_byChar.TryGetValue(characterId, out var list)) return;
            list.RemoveAll(h => h != null && h.titleRef == titleRef);
            if (list.Count == 0) _byChar.Remove(characterId);
        }

        #region 存档

        /// <inheritdoc cref="ISaveable{TState}.GetSaveData"/>
        public List<RuntimeCharacterTitleState> GetSaveData()
        {
            var result = new List<RuntimeCharacterTitleState>(_byChar.Count);
            foreach (var kv in _byChar)
            {
                var st = new RuntimeCharacterTitleState(kv.Key);
                foreach (var h in kv.Value) st.titles.Add(h.Clone());
                result.Add(st);
            }
            return result;
        }

        /// <inheritdoc cref="ISaveable{TState}.LoadSaveData"/>
        public void LoadSaveData(List<RuntimeCharacterTitleState> data)
        {
            _byChar.Clear();
            if (data == null) return;
            foreach (var st in data)
            {
                if (st == null || string.IsNullOrEmpty(st.characterId)) continue;
                var list = new List<TitleHolding>();
                if (st.titles != null)
                    foreach (var h in st.titles)
                        if (h != null && !string.IsNullOrEmpty(h.titleRef))
                            list.Add(h.Clone());
                _byChar[st.characterId] = list;
            }
            // 加载不触发事件。
        }

        /// <inheritdoc cref="ISaveable.ResetAll"/>
        public void ResetAll() => _byChar.Clear();

        #endregion
    }
}
