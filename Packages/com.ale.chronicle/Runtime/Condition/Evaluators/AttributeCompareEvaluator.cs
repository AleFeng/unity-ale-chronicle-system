using System.Collections.Generic;
using Ale.Condition;

namespace Ale.Chronicle
{
    /// <summary>角色判定器：作用域角色某核心属性（当前值）与阈值按 <c>op</c> 比较。键 <c>Chronicle.AttributeCompare</c>。</summary>
    [ConditionEvaluator("Chronicle.AttributeCompare")]
    public sealed class AttributeCompareEvaluator : IConditionEvaluator
    {
        private static readonly ConditionParamDef[] Schema =
        {
            new ConditionParamDef("scope",  ConditionParamType.Int,    false, "作用域", null, ChronicleConditionScopes.Labels),
            new ConditionParamDef("attrId", ConditionParamType.String, false, "属性ID"),
            new ConditionParamDef("op",     ConditionParamType.Int,    false, "比较", null, ChronicleCompareOp.Labels),
            new ConditionParamDef("value",  ConditionParamType.Float,  false, "数值"),
        };

        public string Key => "Chronicle.AttributeCompare";
        public string DisplayName => "属性比较";
        public string Category => "角色";
        public IReadOnlyList<ConditionParamDef> ParamSchema => Schema;

        public bool Evaluate(IReadOnlyList<ConditionParam> parameters, IConditionContext ctx)
        {
            var src = ctx?.GetService<IChronicleConditionSource>();
            if (src == null) return false;
            var scope = (EConditionScope)(int)(parameters.Find("scope")?.GetInt() ?? 0);
            string attrId = parameters.Find("attrId")?.GetString();
            if (string.IsNullOrEmpty(attrId)) return false;
            int op = (int)(parameters.Find("op")?.GetInt() ?? ChronicleCompareOp.GreaterOrEqual);
            double value = parameters.Find("value")?.GetFloat() ?? 0d;
            return ChronicleCompareOp.Compare(src.GetCoreAttribute(scope, attrId), op, value);
        }
    }
}
