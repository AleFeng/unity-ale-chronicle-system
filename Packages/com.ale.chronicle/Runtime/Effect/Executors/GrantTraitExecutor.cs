using System.Collections.Generic;
using Ale.Effect;

namespace Ale.Chronicle
{
    /// <summary>授予特质：durationDays 0 = 按特质定义（临时 → 默认天数 / 永久 → 永久）、&gt;0 指定天数、&lt;0 强制永久；来源标记取当前效果。</summary>
    [EffectExecutor("Chronicle.GrantTrait")]
    public sealed class GrantTraitExecutor : ChronicleExecutorBase
    {
        private static readonly EffectParamDef[] Schema =
        {
            new EffectParamDef("traitId",      EffectParamType.String, false, "特质ID"),
            new EffectParamDef("durationDays", EffectParamType.Float,  false, "持续天数（0 按定义 / >0 指定 / <0 永久）"),
            new EffectParamDef("stacks",       EffectParamType.Int,    false, "层数（<1 视为 1）"),
        };

        public override string Key => "Chronicle.GrantTrait";
        public override string DisplayName => "授予特质";
        public override IReadOnlyList<EffectParamDef> ParamSchema => Schema;

        protected override EffectResult Execute(IReadOnlyList<EffectParam> parameters, IEffectContext ctx,
            string characterId, IChronicleRuntimeSink sink)
        {
            string traitId = Str(parameters, "traitId");
            if (string.IsNullOrEmpty(traitId)) return EffectResult.Failed("traitId 为空");
            float days   = (float)Float(parameters, "durationDays");
            int   stacks = (int)Int(parameters, "stacks");
            return sink.GrantTrait(characterId, traitId, days, stacks < 1 ? 1 : stacks, SourceTagOf(ctx))
                ? EffectResult.Applied
                : EffectResult.Skipped($"未授予特质 '{traitId}'（不存在 / 配置层已持有 / 互斥 / 无有效时长）");
        }
    }
}
