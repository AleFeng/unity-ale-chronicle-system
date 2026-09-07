using System;
using System.Collections.Generic;
using Ale.Effect;
using Ale.GameplayTags;
using Ale.Modifier;
using Ale.Toolkit.Runtime;
using UnityEngine;

namespace Ale.Chronicle
{
    /// <summary>
    /// 效果运行时管理器：每个角色一个 toolkit <see cref="EffectContainer"/>（GAS 式 ASC：活动效果 / 叠加 / 周期 / 抑制 / 授予标签）
    /// + 一份<b>永久落地修饰器</b>列表（瞬时与周期效果的修饰器经 <see cref="IEffectAttributeSink"/> 写入，来源 <c>effect:{id}#{句柄}</c>；
    /// 不改配置基础值，可存档、可追溯）。施加走 <see cref="ChronicleEffectContext"/> + toolkit <see cref="EffectApplier"/>：
    /// 定义按 id 经 <see cref="ChronicleDataManager"/> 解析；属性汇流由 <see cref="ChronicleCharacterRuntime"/> 经 <see cref="CollectModifiers"/> 取用。
    /// 时间推进由 <see cref="ChronicleClock.AdvanceDays"/> 驱动 <see cref="Tick"/>（单位：世界日）。
    /// </summary>
    public class EffectRuntimeManager
        : ToolkitSingleton<EffectRuntimeManager>, ISaveable<RuntimeCharacterEffectState>,
          IEffectContainerSource, IEffectAttributeSink, IEffectAttributeSource, IGameplayTagSource
    {
        private sealed class CharacterEffects
        {
            public readonly EffectContainer          Container;
            public readonly List<ModifierDefinition> Permanent = new List<ModifierDefinition>();

            public CharacterEffects(string characterId, EffectRuntimeManager owner)
            {
                Container = new EffectContainer(characterId)
                {
                    Warning = w => Debug.LogWarning($"[EffectRuntimeManager] {characterId}: {w}"),
                };
                Container.OnEffectAdded            += e        => { owner.OnEffectApplied?.Invoke(characterId, e); owner.OnEffectsChanged?.Invoke(characterId); };
                Container.OnEffectRemoved          += e        => { owner.OnEffectRemoved?.Invoke(characterId, e); owner.OnEffectsChanged?.Invoke(characterId); };
                Container.OnEffectStackChanged     += (e, old) => owner.OnEffectsChanged?.Invoke(characterId);
                Container.OnEffectInhibitedChanged += (e, inh) => owner.OnEffectsChanged?.Invoke(characterId);
                Container.OnPeriodicExecuted       += e        => owner.OnPeriodicExecuted?.Invoke(characterId, e);
                Container.OnModifiersChanged       += ()       => owner.OnModifiersChanged?.Invoke(characterId);
            }
        }

        private readonly Dictionary<string, CharacterEffects> _byChar = new Dictionary<string, CharacterEffects>();

        /// <summary>某角色的活动效果集合 / 层数 / 抑制状态变化。</summary>
        public event Action<string> OnEffectsChanged;

        /// <summary>某角色的属性汇流输入变化（活动效果修饰器 / 永久落地）：UI 重算属性用。</summary>
        public event Action<string> OnModifiersChanged;

        /// <summary>新活动效果实例：(角色 id, 实例)。</summary>
        public event Action<string, ActiveEffect> OnEffectApplied;

        /// <summary>活动效果移除（到期 / 驱散 / 手动）：(角色 id, 实例)。</summary>
        public event Action<string, ActiveEffect> OnEffectRemoved;

        /// <summary>周期结算：(角色 id, 实例)。</summary>
        public event Action<string, ActiveEffect> OnPeriodicExecuted;

        /// <summary>永久修饰器落地：(角色 id, 修饰器)。</summary>
        public event Action<string, ModifierDefinition> OnPermanentModifierApplied;

        /// <summary>线索落地（图标 / 飘字 / 日志）；null = 不派发。演示可挂 <see cref="ChronicleDebugCueSink"/>。</summary>
        public IEffectCueSink CueSink { get; set; }

        /// <summary>执行器落地门面（默认转发各运行时管理器；可替换）。</summary>
        public IChronicleRuntimeSink RuntimeSink { get; set; } = ChronicleRuntimeSink.Instance;

        protected override void Init() { }

        #region 施加 / 移除

        /// <summary>
        /// 对 <paramref name="targetCharacterId"/> 施加效果 <paramref name="effectId"/>（定义经数据管理器解析；
        /// 来源角色进入条件「对象」作用域与执行信息；<paramref name="sourceTag"/> 空 → <c>effect:{id}</c>）。
        /// 返回 toolkit 施加结果（成功 / 叠加 / 刷新 / 各类阻断）。
        /// </summary>
        public EffectApplyResult Apply(string effectId, string targetCharacterId, string sourceCharacterId = null,
            int level = 1, string sourceTag = null, IReadOnlyDictionary<string, float> setByCaller = null)
        {
            if (string.IsNullOrEmpty(effectId) || string.IsNullOrEmpty(targetCharacterId))
                return EffectApplyResult.Blocked(EEffectApplyOutcome.Invalid, "效果 id 或目标角色为空");

            var request = new EffectApplyRequest(effectId, level)
            {
                Source    = sourceCharacterId,
                Target    = targetCharacterId,
                SourceTag = sourceTag,
            };
            if (setByCaller != null)
                foreach (var kv in setByCaller) request.WithSetByCaller(kv.Key, kv.Value);
            return Apply(request);
        }

        /// <summary>按请求施加（<see cref="EffectApplyRequest.Target"/> / <see cref="EffectApplyRequest.Source"/> 须为角色 id 字符串）。</summary>
        public EffectApplyResult Apply(EffectApplyRequest request)
        {
            var target = request?.Target as string;
            if (string.IsNullOrEmpty(target))
                return EffectApplyResult.Blocked(EEffectApplyOutcome.Invalid, "请求目标不是角色 id");
            var ctx = ChronicleEffectContext.Create(target, request.Source as string);
            return EffectApplier.Apply(request, ctx);
        }

        /// <summary>按句柄移除（<paramref name="stacksToRemove"/> -1 = 整个实例）。</summary>
        public bool Remove(string characterId, int handle, int stacksToRemove = -1)
            => TryGetEntry(characterId, out var ce) && ce.Container.RemoveEffect(handle, ChronicleEffectContext.Create(characterId), stacksToRemove);

        /// <summary>移除某角色上所有该 id 的活动效果，返回移除数。</summary>
        public int RemoveById(string characterId, string effectId)
            => TryGetEntry(characterId, out var ce) ? ce.Container.RemoveEffectsById(effectId, ChronicleEffectContext.Create(characterId)) : 0;

        /// <summary>按来源标记移除（如 <c>skill:{id}</c> / <c>effect:{id}</c>），返回移除数。</summary>
        public int RemoveBySourceTag(string characterId, string sourceTag)
            => TryGetEntry(characterId, out var ce) ? ce.Container.RemoveEffectsBySourceTag(sourceTag, ChronicleEffectContext.Create(characterId)) : 0;

        /// <summary>移除资产 / 授予标签命中 <paramref name="tags"/> 的活动效果（驱散），返回移除数。</summary>
        public int RemoveWithTags(string characterId, GameplayTagContainer tags)
            => TryGetEntry(characterId, out var ce) ? ce.Container.RemoveEffectsWithTags(tags, ChronicleEffectContext.Create(characterId)) : 0;

        /// <summary>按单个标签驱散（层级匹配）。</summary>
        public int RemoveWithTag(string characterId, string tag)
        {
            var c = new GameplayTagContainer();
            c.AddTag(tag);
            return RemoveWithTags(characterId, c);
        }

        /// <summary>移除某角色全部活动效果（跑 onRemove；永久落地不回退）。</summary>
        public void RemoveAll(string characterId)
        {
            if (TryGetEntry(characterId, out var ce)) ce.Container.RemoveAll(ChronicleEffectContext.Create(characterId));
        }

        /// <summary>清空某角色的全部运行时效果状态：活动效果（跑 onRemove）、永久落地修饰器、松散标签；随后丢弃容器。</summary>
        public void ClearCharacter(string characterId)
        {
            if (!TryGetEntry(characterId, out var ce)) return;
            ce.Container.RemoveAll(ChronicleEffectContext.Create(characterId));
            ce.Permanent.Clear();
            _byChar.Remove(characterId);
            OnModifiersChanged?.Invoke(characterId);
            OnEffectsChanged?.Invoke(characterId);
        }

        #endregion

        #region 查询

        /// <summary>取角色的效果容器（<paramref name="createIfMissing"/> 为 false 且尚无容器时返回 null）。</summary>
        public EffectContainer GetContainer(string characterId, bool createIfMissing = false)
        {
            if (string.IsNullOrEmpty(characterId)) return null;
            if (_byChar.TryGetValue(characterId, out var ce)) return ce.Container;
            return createIfMissing ? GetOrCreate(characterId).Container : null;
        }

        /// <summary>活动效果实例（只读；无容器返回空）。</summary>
        public IReadOnlyList<ActiveEffect> GetActiveEffects(string characterId)
            => TryGetEntry(characterId, out var ce) ? ce.Container.ActiveEffects : Array.Empty<ActiveEffect>();

        /// <summary>永久落地修饰器（只读；按落地顺序）。</summary>
        public IReadOnlyList<ModifierDefinition> GetPermanentModifiers(string characterId)
            => TryGetEntry(characterId, out var ce) ? ce.Permanent : Array.Empty<ModifierDefinition>();

        /// <summary>是否有该 id 的活动效果。</summary>
        public bool HasEffect(string characterId, string effectId)
            => TryGetEntry(characterId, out var ce) && ce.Container.Find(effectId) != null;

        /// <summary>角色是否拥有标签（效果授予 + 松散标签；层级匹配）。</summary>
        public bool HasTag(string characterId, GameplayTag tag)
            => TryGetEntry(characterId, out var ce) && ce.Container.OwnedTags.HasMatchingTag(tag);

        /// <summary>宿主直接给角色加松散标签（如种族 / 状态标记；参与效果的免疫 / 施加要求 / 抑制判定）。</summary>
        public void AddLooseTag(string characterId, GameplayTag tag, int count = 1)
        {
            if (string.IsNullOrEmpty(characterId) || !tag.IsValid) return;
            GetOrCreate(characterId).Container.AddLooseTag(tag, count);
        }

        public void RemoveLooseTag(string characterId, GameplayTag tag, int count = 1)
        {
            if (TryGetEntry(characterId, out var ce)) ce.Container.RemoveLooseTag(tag, count);
        }

        /// <summary>
        /// 把作用于 <paramref name="attributeId"/> 的活动效果修饰器（未抑制、非周期；已按层数缩放，来源 <c>{sourceTag}#{句柄}</c>）
        /// 与永久落地修饰器（克隆）追加进 <paramref name="into"/>，返回追加数。传空属性 id 收集全部。
        /// </summary>
        public int CollectModifiers(string characterId, string attributeId, List<ModifierDefinition> into)
        {
            if (into == null || !TryGetEntry(characterId, out var ce)) return 0;
            int n = ce.Container.CollectModifiers(attributeId, into);
            bool all = string.IsNullOrEmpty(attributeId);
            foreach (var m in ce.Permanent)
            {
                if (m == null || (!all && m.targetAttributeId != attributeId)) continue;
                into.Add(m.Clone());
                n++;
            }
            return n;
        }

        #endregion

        #region 推进

        /// <summary>推进 <paramref name="days"/> 天：各角色容器 Tick（周期结算先于到期；执行器可能连带改动其它角色，故按快照遍历）。</summary>
        public void Tick(float days)
        {
            if (days <= 0f || _byChar.Count == 0) return;
            var ids = new List<string>(_byChar.Keys);
            foreach (var id in ids)
            {
                if (!_byChar.TryGetValue(id, out var ce) || ce.Container.Count == 0) continue;
                ce.Container.Tick(days, ChronicleEffectContext.Create(id));
            }
        }

        #endregion

        #region toolkit 服务实现

        EffectContainer IEffectContainerSource.GetContainer(object subject)
            => subject is string id && !string.IsNullOrEmpty(id) ? GetOrCreate(id).Container : null;

        void IEffectAttributeSink.ApplyPermanent(object target, string attributeId, EModifierOperation operation, float magnitude, string sourceTag)
        {
            if (!(target is string id) || string.IsNullOrEmpty(id) || string.IsNullOrEmpty(attributeId)) return;
            var m = new ModifierDefinition(attributeId, operation, magnitude, sourceTag) { duration = EModifierDuration.Permanent };
            GetOrCreate(id).Permanent.Add(m);
            OnPermanentModifierApplied?.Invoke(id, m);
            OnModifiersChanged?.Invoke(id);
        }

        bool IEffectAttributeSource.TryGetAttribute(object subject, string attributeId, out float value)
        {
            value = 0f;
            if (!(subject is string id) || string.IsNullOrEmpty(id) || string.IsNullOrEmpty(attributeId)) return false;
            if (ChronicleDataManager.Instance?.GetCoreAttribute(attributeId) == null) return false;
            value = ChronicleCharacterRuntime.EvaluateValue(id, attributeId);
            return true;
        }

        GameplayTagCountContainer IGameplayTagSource.GetOwnedTags(object subject)
            => subject is string id && TryGetEntry(id, out var ce) ? ce.Container.OwnedTags : null;

        #endregion

        #region 存档

        public List<RuntimeCharacterEffectState> GetSaveData()
        {
            var result = new List<RuntimeCharacterEffectState>(_byChar.Count);
            var tags   = new List<GameplayTag>();
            foreach (var kv in _byChar)
            {
                var ce = kv.Value;
                tags.Clear();
                ce.Container.OwnedTags.GetExplicitTags(tags);
                if (ce.Container.Count == 0 && ce.Permanent.Count == 0 && tags.Count == 0) continue;   // 空壳不落盘
                var st = new RuntimeCharacterEffectState(kv.Key) { container = ce.Container.ExportState() };
                foreach (var m in ce.Permanent) st.permanentModifiers.Add(m.Clone());
                result.Add(st);
            }
            return result;
        }

        /// <summary>恢复（覆盖语义）：容器经 <see cref="EffectContainer.ImportState"/> 静默重建（不跑阶段不发事件），定义按数据管理器解析。</summary>
        public void LoadSaveData(List<RuntimeCharacterEffectState> data)
        {
            _byChar.Clear();
            if (data == null) return;
            var defs = ChronicleDataManager.Instance;
            foreach (var st in data)
            {
                if (st == null || string.IsNullOrEmpty(st.characterId)) continue;
                var ce = GetOrCreate(st.characterId);
                ce.Container.ImportState(st.container, defs, ChronicleEffectContext.Create(st.characterId));
                if (st.permanentModifiers != null)
                    foreach (var m in st.permanentModifiers)
                        if (m != null && !string.IsNullOrEmpty(m.targetAttributeId)) ce.Permanent.Add(m.Clone());
            }
            // 加载不触发事件。
        }

        public void ResetAll() => _byChar.Clear();

        #endregion

        #region 内部

        private CharacterEffects GetOrCreate(string characterId)
        {
            if (!_byChar.TryGetValue(characterId, out var ce))
                _byChar[characterId] = ce = new CharacterEffects(characterId, this);
            return ce;
        }

        private bool TryGetEntry(string characterId, out CharacterEffects ce)
        {
            ce = null;
            return !string.IsNullOrEmpty(characterId) && _byChar.TryGetValue(characterId, out ce);
        }

        #endregion
    }
}
