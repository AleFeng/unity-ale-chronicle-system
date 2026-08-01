using System.Collections.Generic;
using Ale.Condition;

namespace Ale.Chronicle
{
    /// <summary>角色判定器：作用域角色是否拥有某特质。键 <c>Chronicle.HasTrait</c>。</summary>
    [ConditionEvaluator("Chronicle.HasTrait")]
    public sealed class HasTraitEvaluator : IConditionEvaluator
    {
        private static readonly ConditionParamDef[] Schema =
        {
            new ConditionParamDef("scope",   ConditionParamType.Int,    false, "作用域", null, ChronicleConditionScopes.Labels),
            new ConditionParamDef("traitId", ConditionParamType.String, false, "特质ID"),
        };

        public string Key => "Chronicle.HasTrait";
        public string DisplayName => "拥有特质";
        public string Category => "角色";
        public IReadOnlyList<ConditionParamDef> ParamSchema => Schema;

        public bool Evaluate(IReadOnlyList<ConditionParam> parameters, IConditionContext ctx)
        {
            var src = ctx?.GetService<IChronicleConditionSource>();
            if (src == null) return false;
            var scope = (EConditionScope)(int)(parameters.Find("scope")?.GetInt() ?? 0);
            string traitId = parameters.Find("traitId")?.GetString();
            return !string.IsNullOrEmpty(traitId) && src.HasTrait(scope, traitId);
        }
    }
}
