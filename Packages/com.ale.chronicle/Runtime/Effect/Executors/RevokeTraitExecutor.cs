using System.Collections.Generic;
using Ale.Effect;

namespace Ale.Chronicle
{
    /// <summary>移除运行时特质（配置层特质不受影响）。</summary>
    [EffectExecutor("Chronicle.RevokeTrait")]
    public sealed class RevokeTraitExecutor : ChronicleExecutorBase
    {
        private static readonly EffectParamDef[] Schema =
        {
            new EffectParamDef("traitId", EffectParamType.String, false, "特质ID"),
        };

        public override string Key => "Chronicle.RevokeTrait";
        public override string DisplayName => "移除特质";
        public override IReadOnlyList<EffectParamDef> ParamSchema => Schema;

        protected override EffectResult Execute(IReadOnlyList<EffectParam> parameters, IEffectContext ctx,
            string characterId, IChronicleRuntimeSink sink)
        {
            string traitId = Str(parameters, "traitId");
            if (string.IsNullOrEmpty(traitId)) return EffectResult.Failed("traitId 为空");
            return sink.RevokeTrait(characterId, traitId)
                ? EffectResult.Applied
                : EffectResult.Skipped($"未持有运行时特质 '{traitId}'");
        }
    }
}
