using System.Collections.Generic;
using Ale.Effect;

namespace Ale.Chronicle
{
    /// <summary>授予头衔（经 <see cref="TitleRuntimeManager"/>：阶级头衔晋升替换、唯一头衔易主等规则照旧；获得日取世界时钟）。</summary>
    [EffectExecutor("Chronicle.GrantTitle")]
    public sealed class GrantTitleExecutor : ChronicleExecutorBase
    {
        private static readonly EffectParamDef[] Schema =
        {
            new EffectParamDef("titleId", EffectParamType.String, false, "头衔ID"),
        };

        public override string Key => "Chronicle.GrantTitle";
        public override string DisplayName => "授予头衔";
        public override IReadOnlyList<EffectParamDef> ParamSchema => Schema;

        protected override EffectResult Execute(IReadOnlyList<EffectParam> parameters, IEffectContext ctx,
            string characterId, IChronicleRuntimeSink sink)
        {
            string titleId = Str(parameters, "titleId");
            if (string.IsNullOrEmpty(titleId)) return EffectResult.Failed("titleId 为空");
            return sink.GrantTitle(characterId, titleId)
                ? EffectResult.Applied
                : EffectResult.Skipped($"未授予头衔 '{titleId}'（已持有 / 不存在）");
        }
    }
}
