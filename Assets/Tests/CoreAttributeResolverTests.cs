using System.Collections.Generic;
using NUnit.Framework;
using Ale.Chronicle;
using Ale.Toolkit.Runtime;

namespace Ale.Chronicle.Tests
{
    /// <summary>
    /// 属性系统与修饰器汇流门槛：Resolver 正确收集多来源修饰器、按 def 的 min/max 夹取、
    /// 与 toolkit 求值器结果一致、来源明细正确；以及 Text/Sprite 字段默认类型与深拷贝。
    /// </summary>
    public class CoreAttributeResolverTests
    {
        private static CoreAttributeDefinition Def(float min = 0f, float max = 100f, float defBase = 10f)
            => new CoreAttributeDefinition("str") { minValue = min, maxValue = max, defaultBase = defBase };

        private static ModifierDefinition Mod(EModifierOperation op, float mag, string src)
            => new ModifierDefinition("str", op, mag, src);

        [Test]
        public void NoModifiers_ReturnsBase()
        {
            var e = CoreAttributeResolver.Evaluate(Def(), 10f, new List<ModifierDefinition>());
            Assert.AreEqual(10f, e.Value, 1e-4f);
        }

        [Test]
        public void CollectsMultipleSources_WithBreakdown()
        {
            var mods = new List<ModifierDefinition>
            {
                Mod(EModifierOperation.Add, 2f, "race:elf"),
                Mod(EModifierOperation.Add, 3f, "prof:warrior:growth"),
                Mod(EModifierOperation.PercentAdd, 0.5f, "trait:力大无穷"),
            };
            var e = CoreAttributeResolver.Evaluate(Def(), 10f, mods);

            // base 10 +5 = 15，×(1+0.5) = 22.5
            Assert.AreEqual(22.5f, e.Value, 1e-3f);
            Assert.AreEqual(3, e.Breakdown.Count);

            float sum = 0f;
            foreach (var c in e.Breakdown) sum += c.Delta;
            Assert.AreEqual(e.RawValue - e.BaseValue, sum, 1e-3f);
        }

        [Test]
        public void RespectsDefMinMax()
        {
            var e1 = CoreAttributeResolver.Evaluate(Def(0f, 20f), 10f,
                new List<ModifierDefinition> { Mod(EModifierOperation.Add, 1000f, "buff") });
            Assert.AreEqual(20f, e1.Value, 1e-4f);   // 夹到 def.max

            var e2 = CoreAttributeResolver.Evaluate(Def(5f, 100f), 10f,
                new List<ModifierDefinition> { Mod(EModifierOperation.Add, -1000f, "curse") });
            Assert.AreEqual(5f, e2.Value, 1e-4f);    // 夹到 def.min
        }

        [Test]
        public void DefaultBaseOverload_UsesDefDefaultBase()
        {
            var e = CoreAttributeResolver.Evaluate(Def(0f, 100f, 42f), new List<ModifierDefinition>());
            Assert.AreEqual(42f, e.Value, 1e-4f);
        }

        [Test]
        public void CoreAttributeValueOverload_UsesItsBase()
        {
            var v = new CoreAttributeValue("str", 30f);
            var e = CoreAttributeResolver.Evaluate(Def(0f, 100f), v,
                new List<ModifierDefinition> { Mod(EModifierOperation.Add, 5f, "x") });
            Assert.AreEqual(35f, e.Value, 1e-4f);
        }

        [Test]
        public void MatchesToolkitEvaluatorDirectly()
        {
            var mods = new List<ModifierDefinition> { Mod(EModifierOperation.Add, 7f, "x") };
            float viaResolver = CoreAttributeResolver.Evaluate(Def(0f, 100f), 10f, mods).Value;
            float viaToolkit  = ModifierStackEvaluator.EvaluateValue(10f, 0f, 100f, mods);
            Assert.AreEqual(viaToolkit, viaResolver, 1e-4f);
        }

        [Test]
        public void TypedFields_DefaultToCorrectType()
        {
            var d = new CoreAttributeDefinition("str");
            Assert.AreEqual(EFieldType.Text,   d.displayName.Type);
            Assert.AreEqual(EFieldType.Text,   d.abbreviation.Type);
            Assert.AreEqual(EFieldType.Text,   d.description.Type);
            Assert.AreEqual(EFieldType.Sprite, d.icon.Type);
        }

        [Test]
        public void PlainName_FallsBackToId()
        {
            var d = new CoreAttributeDefinition("力量");
            Assert.AreEqual("力量", d.PlainName());
            d.displayName.SetTextValue(0, "力量属性");
            Assert.AreEqual("力量属性", d.PlainName());
        }

        [Test]
        public void Clone_IsDeep()
        {
            var d = new CoreAttributeDefinition("str") { minValue = 1f, maxValue = 9f, defaultBase = 5f };
            d.displayName.SetTextValue(0, "Strength");

            var c = d.Clone();
            c.displayName.SetTextValue(0, "Changed");

            Assert.AreEqual("Strength", d.displayName.GetTextValue()); // 原对象不受影响
            Assert.AreEqual(9f, c.maxValue, 1e-4f);
        }
    }
}
