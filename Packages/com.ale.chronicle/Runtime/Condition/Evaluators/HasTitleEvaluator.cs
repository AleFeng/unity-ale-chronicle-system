using System.Collections.Generic;
using Ale.Condition;

namespace Ale.Chronicle
{
    /// <summary>角色判定器：作用域角色是否持有某头衔。键 <c>Chronicle.HasTitle</c>。</summary>
    [ConditionEvaluator("Chronicle.HasTitle")]
    public sealed class HasTitleEvaluator : IConditionEvaluator
    {
        private static readonly ConditionParamDef[] Schema =
        {
            new ConditionParamDef("scope",   ConditionParamType.Int,    false, "作用域", null, ChronicleConditionScopes.Labels),
            new ConditionParamDef("titleId", ConditionParamType.String, false, "头衔ID"),
        };

        public string Key => "Chronicle.HasTitle";
        public string DisplayName => "持有头衔";
        public string Category => "角色";
        public IReadOnlyList<ConditionParamDef> ParamSchema => Schema;

        public bool Evaluate(IReadOnlyList<ConditionParam> parameters, IConditionContext ctx)
        {
            var src = ctx?.GetService<IChronicleConditionSource>();
            if (src == null) return false;
            var scope = (EConditionScope)(int)(parameters.Find("scope")?.GetInt() ?? 0);
            string titleId = parameters.Find("titleId")?.GetString();
            return !string.IsNullOrEmpty(titleId) && src.HasTitle(scope, titleId);
        }
    }
}
