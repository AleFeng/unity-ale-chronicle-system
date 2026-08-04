using System.Collections.Generic;
using Ale.Condition;

namespace Ale.Chronicle
{
    /// <summary>角色判定器：作用域角色在某阶级序列上的最高位阶(rankTier) ≥ minTier。键 <c>Chronicle.HasRankAtLeast</c>。</summary>
    [ConditionEvaluator("Chronicle.HasRankAtLeast")]
    public sealed class HasRankAtLeastEvaluator : IConditionEvaluator
    {
        private static readonly ConditionParamDef[] Schema =
        {
            new ConditionParamDef("scope",    ConditionParamType.Int,    false, "作用域", null, ChronicleConditionScopes.Labels),
            new ConditionParamDef("ladderId", ConditionParamType.String, false, "阶级序列ID"),
            new ConditionParamDef("minTier",  ConditionParamType.Int,    false, "最低位阶"),
        };

        public string Key => "Chronicle.HasRankAtLeast";
        public string DisplayName => "位阶达到";
        public string Category => "角色";
        public IReadOnlyList<ConditionParamDef> ParamSchema => Schema;

        public bool Evaluate(IReadOnlyList<ConditionParam> parameters, IConditionContext ctx)
        {
            var src = ctx?.GetService<IChronicleConditionSource>();
            if (src == null) return false;
            var scope = (EConditionScope)(int)(parameters.Find("scope")?.GetInt() ?? 0);
            string ladderId = parameters.Find("ladderId")?.GetString();
            if (string.IsNullOrEmpty(ladderId)) return false;
            int minTier = (int)(parameters.Find("minTier")?.GetInt() ?? 0);
            return src.GetHighestRankTier(scope, ladderId) >= minTier;
        }
    }
}
