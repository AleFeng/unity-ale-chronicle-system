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
            ConditionCompare.CreateOpParam(),
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
            int op = ConditionCompare.ReadOp(parameters);
            // 局部变量叫 amount 而不是 value：它是「阈值」，与 ConditionCompare.Compare 的第一个形参
            // value（被测值）同名不同义，最容易把两个 double 实参传反。参数 id 仍是 "value"（已序列化，不能改）。
            double amount = parameters.Find("value")?.GetFloat() ?? 0d;
            return ConditionCompare.Compare(src.GetCoreAttribute(scope, attrId), amount, op);
        }
    }
}
