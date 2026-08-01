using System.Collections.Generic;
using Ale.Condition;

namespace Ale.Chronicle
{
    /// <summary>角色判定器：作用域角色年龄在 [min, max] 内（max = -1 表示无上限）。键 <c>Chronicle.Age</c>。</summary>
    [ConditionEvaluator("Chronicle.Age")]
    public sealed class AgeEvaluator : IConditionEvaluator
    {
        private static readonly ConditionParamDef[] Schema =
        {
            new ConditionParamDef("scope", ConditionParamType.Int, false, "作用域", null, ChronicleConditionScopes.Labels),
            new ConditionParamDef("min",   ConditionParamType.Int, false, "最小年龄"),
            new ConditionParamDef("max",   ConditionParamType.Int, false, "最大年龄(-1=无上限)"),
        };

        public string Key => "Chronicle.Age";
        public string DisplayName => "年龄区间";
        public string Category => "角色";
        public IReadOnlyList<ConditionParamDef> ParamSchema => Schema;

        public bool Evaluate(IReadOnlyList<ConditionParam> parameters, IConditionContext ctx)
        {
            var src = ctx?.GetService<IChronicleConditionSource>();
            if (src == null) return false;
            var scope = (EConditionScope)(int)(parameters.Find("scope")?.GetInt() ?? 0);
            int min = (int)(parameters.Find("min")?.GetInt() ?? 0);
            int max = (int)(parameters.Find("max")?.GetInt() ?? -1);
            int age = src.GetAge(scope);
            return age >= min && (max < 0 || age <= max);
        }
    }
}
