using System.Collections.Generic;
using Ale.Effect;

namespace Ale.Chronicle
{
    /// <summary>剥夺头衔（受头衔定义 isRevocable 约束）。</summary>
    [EffectExecutor("Chronicle.RevokeTitle")]
    public sealed class RevokeTitleExecutor : ChronicleExecutorBase
    {
        private static readonly EffectParamDef[] Schema =
        {
            new EffectParamDef("titleId", EffectParamType.String, false, "头衔ID"),
        };

        public override string Key => "Chronicle.RevokeTitle";
        public override string DisplayName => "剥夺头衔";
        public override IReadOnlyList<EffectParamDef> ParamSchema => Schema;

        protected override EffectResult Execute(IReadOnlyList<EffectParam> parameters, IEffectContext ctx,
            string characterId, IChronicleRuntimeSink sink)
        {
            string titleId = Str(parameters, "titleId");
            if (string.IsNullOrEmpty(titleId)) return EffectResult.Failed("titleId 为空");
            return sink.RevokeTitle(characterId, titleId)
                ? EffectResult.Applied
                : EffectResult.Skipped($"未剥夺头衔 '{titleId}'（未持有 / 不可剥夺）");
        }
    }
}
