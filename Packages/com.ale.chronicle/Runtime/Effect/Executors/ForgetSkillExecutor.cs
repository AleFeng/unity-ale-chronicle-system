using System.Collections.Generic;
using Ale.Effect;

namespace Ale.Chronicle
{
    /// <summary>遗忘技能（永久层；提供层不受影响）。</summary>
    [EffectExecutor("Chronicle.ForgetSkill")]
    public sealed class ForgetSkillExecutor : ChronicleExecutorBase
    {
        private static readonly EffectParamDef[] Schema =
        {
            new EffectParamDef("skillId", EffectParamType.String, false, "技能ID"),
        };

        public override string Key => "Chronicle.ForgetSkill";
        public override string DisplayName => "遗忘技能";
        public override IReadOnlyList<EffectParamDef> ParamSchema => Schema;

        protected override EffectResult Execute(IReadOnlyList<EffectParam> parameters, IEffectContext ctx,
            string characterId, IChronicleRuntimeSink sink)
        {
            string skillId = Str(parameters, "skillId");
            if (string.IsNullOrEmpty(skillId)) return EffectResult.Failed("skillId 为空");
            return sink.ForgetSkill(characterId, skillId)
                ? EffectResult.Applied
                : EffectResult.Skipped($"未学会技能 '{skillId}'");
        }
    }
}
