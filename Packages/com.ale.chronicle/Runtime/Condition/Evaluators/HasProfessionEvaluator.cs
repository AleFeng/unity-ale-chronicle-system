using System.Collections.Generic;
using Ale.Condition;

namespace Ale.Chronicle
{
    /// <summary>角色判定器：作用域角色是否从事某职业。键 <c>Chronicle.HasProfession</c>。</summary>
    [ConditionEvaluator("Chronicle.HasProfession")]
    public sealed class HasProfessionEvaluator : IConditionEvaluator
    {
        private static readonly ConditionParamDef[] Schema =
        {
            new ConditionParamDef("scope",        ConditionParamType.Int,    false, "作用域", null, ChronicleConditionScopes.Labels),
            new ConditionParamDef("professionId", ConditionParamType.String, false, "职业ID"),
        };

        public string Key => "Chronicle.HasProfession";
        public string DisplayName => "拥有职业";
        public string Category => "角色";
        public IReadOnlyList<ConditionParamDef> ParamSchema => Schema;

        public bool Evaluate(IReadOnlyList<ConditionParam> parameters, IConditionContext ctx)
        {
            var src = ctx?.GetService<IChronicleConditionSource>();
            if (src == null) return false;
            var scope = (EConditionScope)(int)(parameters.Find("scope")?.GetInt() ?? 0);
            string professionId = parameters.Find("professionId")?.GetString();
            return !string.IsNullOrEmpty(professionId) && src.HasProfession(scope, professionId);
        }
    }
}
