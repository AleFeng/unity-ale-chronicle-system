using NUnit.Framework;
using Ale.Chronicle;
using Ale.Toolkit.Runtime;
using Ale.Condition;

namespace Ale.Chronicle.Tests
{
    /// <summary>
    /// 技能树扩展 S5 求值路径门槛：CoreAttributeResolver 新 4 参重载在收集期按条件过滤
    /// CoreAttributeDefinition.conditionalModifiers——空条件恒计入、非空条件在无上下文时排除；
    /// 旧 3 参重载委托到 ctx=null，无 conditionalModifiers 时汇流结果不变（零回归）。
    /// </summary>
    public class AttributeConditionalModifierTests
    {
        private static CoreAttributeDefinition NewAttr()
            => new CoreAttributeDefinition("interest") { minValue = 0f, maxValue = 100f, defaultBase = 10f };

        private static void AddAgeAtLeast(ConditionExpression expr, int min)
        {
            var g = new ConditionGroup { itemOperator = ConditionLogicOp.And };
            var it = new ConditionItem("Chronicle.Age");
            var p = new ConditionParam("min", ConditionParamType.Int); p.SetInt(min);
            it.parameters.Add(p);
            g.items.Add(it);
            expr.groups.Add(g);
        }

        [Test]
        public void EmptyCondition_ConditionalModifier_AppliesInPreview()
        {
            var def = NewAttr();
            def.conditionalModifiers.Add(new ConditionalModifier
            {
                modifier = new ModifierDefinition("interest", EModifierOperation.Add, 5f, null),
            });
            // ctx=null（编辑器预览）；空条件恒通过 → 计入
            var e = CoreAttributeResolver.Evaluate(null, def, null, null);
            Assert.AreEqual(15f, e.Value, 1e-4f);
        }

        [Test]
        public void NonEmptyCondition_ConditionalModifier_ExcludedWithoutContext()
        {
            var def = NewAttr();
            var cm = new ConditionalModifier
            {
                modifier = new ModifierDefinition("interest", EModifierOperation.Add, 5f, null),
            };
            AddAgeAtLeast(cm.condition, 20);
            def.conditionalModifiers.Add(cm);
            // 非空条件 + ctx=null → 判定器取不到数据源 → 不通过 → 不计入
            var e = CoreAttributeResolver.Evaluate(null, def, null, null);
            Assert.AreEqual(10f, e.Value, 1e-4f);
        }

        [Test]
        public void ThreeArgOverload_NoConditionalModifiers_UnchangedRegression()
        {
            var def = NewAttr();
            // 无 conditionalModifiers；3 参重载（委托 ctx=null）应与旧行为一致：仅基础值
            var e = CoreAttributeResolver.Evaluate(null, def, null);
            Assert.AreEqual(10f, e.Value, 1e-4f);
        }
    }
}
