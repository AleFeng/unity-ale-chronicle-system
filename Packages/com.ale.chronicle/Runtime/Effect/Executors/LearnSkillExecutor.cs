using System.Collections.Generic;
using Ale.Effect;

namespace Ale.Chronicle
{
    /// <summary>学会技能（永久层）。</summary>
    [EffectExecutor("Chronicle.LearnSkill")]
    public sealed class LearnSkillExecutor : ChronicleExecutorBase
    {
        private static readonly EffectParamDef[] Schema =
        {
            new EffectParamDef("skillId", EffectParamType.String, false, "技能ID"),
        };

        public override string Key => "Chronicle.LearnSkill";
        public override string DisplayName => "学会技能";
        public override IReadOnlyList<EffectParamDef> ParamSchema => Schema;

        protected override EffectResult Execute(IReadOnlyList<EffectParam> parameters, IEffectContext ctx,
            string characterId, IChronicleRuntimeSink sink)
        {
            string skillId = Str(parameters, "skillId");
            if (string.IsNullOrEmpty(skillId)) return EffectResult.Failed("skillId 为空");
            return sink.LearnSkill(characterId, skillId)
                ? EffectResult.Applied
                : EffectResult.Skipped($"已学会技能 '{skillId}'");
        }
    }
}
