using System;
using System.Collections.Generic;
using Ale.Effect;

namespace Ale.Chronicle
{
    /// <summary>
    /// 技能「使用 / 施放」派发事件：目标角色、技能、可选来源键（装备 / UI 等）、可选来源角色（施放者），
    /// 以及本次按序施加 <see cref="Skill.onUseEffectRefs"/> 的逐个结果（无引用 → 空列表）。
    /// </summary>
    public readonly struct SkillUseEvent
    {
        private readonly IReadOnlyList<EffectApplyResult> _effectResults;

        /// <summary>目标角色 id。</summary>
        public readonly string TargetCharacterId;

        /// <summary>技能 id。</summary>
        public readonly string SkillId;

        /// <summary>来源键（调用方自定，如装备 / UI 入口；可空）。</summary>
        public readonly string SourceKey;

        /// <summary>来源角色 id（施放者；可空）。进入效果的来源与条件「对象」作用域。</summary>
        public readonly string SourceCharacterId;

        /// <summary>逐效果施加结果（顺序同 onUseEffectRefs；永不为 null）。</summary>
        public IReadOnlyList<EffectApplyResult> EffectResults => _effectResults ?? Array.Empty<EffectApplyResult>();

        /// <summary>成功施加（含叠加 / 刷新）的效果数。</summary>
        public int EffectsApplied
        {
            get
            {
                int n = 0;
                foreach (var r in EffectResults) if (r.IsSuccess) n++;
                return n;
            }
        }

        public SkillUseEvent(string targetCharacterId, string skillId, string sourceKey)
            : this(targetCharacterId, skillId, sourceKey, null, null) { }

        public SkillUseEvent(string targetCharacterId, string skillId, string sourceKey, string sourceCharacterId,
            IReadOnlyList<EffectApplyResult> effectResults)
        {
            TargetCharacterId = targetCharacterId;
            SkillId           = skillId;
            SourceKey         = sourceKey;
            SourceCharacterId = sourceCharacterId;
            _effectResults    = effectResults;
        }
    }
}
