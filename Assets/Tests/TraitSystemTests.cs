using System.Collections.Generic;
using NUnit.Framework;
using Ale.Chronicle;
using Ale.Toolkit.Runtime;
using Ale.Condition;

namespace Ale.Chronicle.Tests
{
    /// <summary>
    /// 特质系统门槛：特质 modifiers 经 CollectModifiers 打来源标记后汇入 CoreAttributeResolver（闭环）；
    /// 收集按 targetAttributeId 过滤且不改配置本体；临时特质剩余天数语义；硬互斥（组 + 显式列表）校验；
    /// eligibility(ConditionExpression) JSON 往返与深拷贝独立。
    /// </summary>
    public class TraitSystemTests
    {
        private static CoreAttributeDefinition StrDef(float min = 0f, float max = 100f)
            => new CoreAttributeDefinition("str") { minValue = min, maxValue = max, defaultBase = 10f };

        private static TraitDefinition Trait(string id, params ModifierDefinition[] mods)
        {
            var t = new TraitDefinition(id);
            if (mods != null) t.modifiers.AddRange(mods);
            return t;
        }

        private static ModifierDefinition Mod(string target, EModifierOperation op, float mag, string src = null)
            => new ModifierDefinition(target, op, mag, src);

        [Test]
        public void TraitModifiers_FlowIntoResolver_WithSourceTags()
        {
            var brawn = Trait("力大无穷", Mod("str", EModifierOperation.Add, 5f));       // 无自带 sourceTag
            var blessed = Trait("神眷",   Mod("str", EModifierOperation.PercentAdd, 0.2f));

            var mods = new List<ModifierDefinition>();
            brawn.CollectModifiers("str", mods);
            blessed.CollectModifiers("str", mods);

            var e = CoreAttributeResolver.Evaluate(StrDef(), 10f, mods);
            // (10 + 5) × (1 + 0.2) = 18
            Assert.AreEqual(18f, e.Value, 1e-3f);

            var tags = new HashSet<string>();
            foreach (var c in e.Breakdown) tags.Add(c.SourceTag);
            Assert.Contains("trait:力大无穷", new List<string>(tags));
            Assert.Contains("trait:神眷",     new List<string>(tags));
        }

        [Test]
        public void CollectModifiers_FiltersByTarget_KeepsExplicitTag_DoesNotMutateSource()
        {
            var t = Trait("混合",
                Mod("str", EModifierOperation.Add, 3f),
                Mod("dex", EModifierOperation.Add, 9f),                       // 不同目标，应被过滤
                Mod("str", EModifierOperation.Add, 2f, "trait:混合:special")); // 自带来源，应保留

            var strOnly = new List<ModifierDefinition>();
            t.CollectModifiers("str", strOnly);

            Assert.AreEqual(2, strOnly.Count);                 // 只收 str 目标
            Assert.AreEqual("trait:混合",         strOnly[0].sourceTag); // 无来源者补默认
            Assert.AreEqual("trait:混合:special", strOnly[1].sourceTag); // 自带来源保留

            // 配置本体的原始 modifier 未被改写
            Assert.IsNull(t.modifiers[0].sourceTag);

            var all = new List<ModifierDefinition>();
            t.CollectModifiers(null, all);
            Assert.AreEqual(3, all.Count);                     // 空 target = 全收
        }

        [Test]
        public void TemporaryTrait_DurationSemantics()
        {
            var permanent = new CharacterTraitInstance("勇敢");                  // remainingDays = -1
            Assert.IsTrue(permanent.IsPermanent);
            Assert.IsFalse(permanent.IsExpired);
            Assert.AreEqual(-1f, permanent.Ticked(999f).remainingDays, 1e-4f);  // 永久不衰减

            var temp = new CharacterTraitInstance("醉酒", 3f);
            Assert.IsFalse(temp.IsPermanent);

            var after2 = temp.Ticked(2f);
            Assert.AreEqual(1f, after2.remainingDays, 1e-4f);
            Assert.IsFalse(after2.IsExpired);

            var after5 = after2.Ticked(5f);                                     // 夹到 0，不为负
            Assert.AreEqual(0f, after5.remainingDays, 1e-4f);
            Assert.IsTrue(after5.IsExpired);
        }

        [Test]
        public void Incompatibility_ByGroupAndExplicitList()
        {
            var brave  = new TraitDefinition("brave")  { groupEquivalenceRef = "勇怯" };
            var craven = new TraitDefinition("craven") { groupEquivalenceRef = "勇怯" };
            Assert.IsTrue(brave.IsIncompatibleWith(craven));   // 同组互斥
            Assert.IsTrue(craven.IsIncompatibleWith(brave));   // 双向

            var a = new TraitDefinition("a");
            var b = new TraitDefinition("b");
            b.incompatibleTraitRefs.Add("a");
            Assert.IsTrue(a.IsIncompatibleWith(b));            // 反向声明也生效
            Assert.IsTrue(b.IsIncompatibleWith(a));

            Assert.IsFalse(brave.IsIncompatibleWith(a));       // 无关
            Assert.IsFalse(brave.IsIncompatibleWith(brave));   // 自身
            Assert.IsFalse(brave.IsIncompatibleWith(null));    // null
        }

        [Test]
        public void Eligibility_JsonRoundTrip()
        {
            var t = new TraitDefinition("将才");
            var g = new ConditionGroup { itemOperator = ConditionLogicOp.And };
            var it = new ConditionItem("Chronicle.Age");
            var min = new ConditionParam("min", ConditionParamType.Int); min.SetInt(18);
            it.parameters.Add(min);
            g.items.Add(it);
            t.eligibility.groups.Add(g);

            Assert.AreEqual(1, t.eligibility.TotalItemCount());

            string json = ConditionJson.ToJson(t.eligibility);
            var back = ConditionJson.FromJson(json);

            Assert.AreEqual(1, back.TotalItemCount());
            Assert.AreEqual("Chronicle.Age", back.groups[0].items[0].key);
            Assert.AreEqual(18L, back.groups[0].items[0].parameters.Find("min").GetInt());
        }

        [Test]
        public void Clone_IsDeep_EligibilityAndModifiersIndependent()
        {
            var t = Trait("t", Mod("str", EModifierOperation.Add, 5f));
            t.displayName.SetTextValue(0, "特质");
            t.eligibility.groups.Add(new ConditionGroup());

            var c = t.Clone();
            c.displayName.SetTextValue(0, "改了");
            c.modifiers[0].magnitude = 99f;
            c.eligibility.groups.Clear();

            Assert.AreEqual("特质", t.displayName.GetTextValue()); // 文本独立
            Assert.AreEqual(5f, t.modifiers[0].magnitude, 1e-4f);  // 修饰器独立
            Assert.AreEqual(1, t.eligibility.groups.Count);        // 条件独立
        }

        [Test]
        public void TypedFields_DefaultToCorrectType()
        {
            var t = new TraitDefinition("t");
            Assert.AreEqual(EFieldType.Text,   t.displayName.Type);
            Assert.AreEqual(EFieldType.Text,   t.description.Type);
            Assert.AreEqual(EFieldType.Sprite, t.icon.Type);
            Assert.IsNotNull(t.eligibility);        // eligibility 默认已实例化
            Assert.IsTrue(t.eligibility.IsEmpty);   // 默认空 = 无门槛
        }
    }
}
