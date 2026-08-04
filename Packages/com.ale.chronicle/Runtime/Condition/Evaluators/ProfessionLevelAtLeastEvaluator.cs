using System.Collections.Generic;
using Ale.Condition;

namespace Ale.Chronicle
{
    /// <summary>角色判定器：作用域角色某职业等级 ≥ minLevel。键 <c>Chronicle.ProfessionLevelAtLeast</c>。</summary>
    [ConditionEvaluator("Chronicle.ProfessionLevelAtLeast")]
    public sealed class ProfessionLevelAtLeastEvaluator : IConditionEvaluator
    {
        private static readonly ConditionParamDef[] Schema =
        {
            new ConditionParamDef("scope",        ConditionParamType.Int,    false, "作用域", null, ChronicleConditionScopes.Labels),
            new ConditionParamDef("professionId", ConditionParamType.String, false, "职业ID"),
            new ConditionParamDef("minLevel",     ConditionParamType.Int,    false, "最低等级"),
        };

        public string Key => "Chronicle.ProfessionLevelAtLeast";
        public string DisplayName => "职业等级达到";
        public string Category => "角色";
        public IReadOnlyList<ConditionParamDef> ParamSchema => Schema;

        public bool Evaluate(IReadOnlyList<ConditionParam> parameters, IConditionContext ctx)
        {
            var src = ctx?.GetService<IChronicleConditionSource>();
            if (src == null) return false;
            var scope = (EConditionScope)(int)(parameters.Find("scope")?.GetInt() ?? 0);
            string professionId = parameters.Find("professionId")?.GetString();
            if (string.IsNullOrEmpty(professionId)) return false;
            int minLevel = (int)(parameters.Find("minLevel")?.GetInt() ?? 0);
            return src.GetProfessionLevel(scope, professionId) >= minLevel;
        }
    }
}
