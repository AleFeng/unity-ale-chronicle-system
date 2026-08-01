using System.Collections.Generic;
using NUnit.Framework;
using Ale.Chronicle;
using Ale.Toolkit.Runtime;

namespace Ale.Chronicle.Tests
{
    /// <summary>
    /// 顶层组合装配门槛：RebuildAttributes 对账「模板 ∪ 特质功能标签」字段（来源优先级 / 增删）、
    /// GetAge = 世界日 − 生日（夹到 0）、端到端组合求值（带 2 特质角色 → CoreAttributeResolver
    /// 得含来源明细的最终属性）、核心基础值回退、深拷贝独立。
    /// </summary>
    public class CharacterCompositionTests
    {
        private sealed class FakeSchema : IChronicleSchemaSource
        {
            public readonly Dictionary<string, CharacterTemplate> Templates = new Dictionary<string, CharacterTemplate>();
            public readonly Dictionary<string, TraitDefinition>   Traits    = new Dictionary<string, TraitDefinition>();
            public readonly Dictionary<string, Tag>               Tags      = new Dictionary<string, Tag>();

            public CharacterTemplate GetCharacterTemplate(string name) => name != null && Templates.TryGetValue(name, out var t) ? t : null;
            public TraitDefinition   GetTrait(string id)               => id   != null && Traits.TryGetValue(id, out var t)      ? t : null;
            public Tag               GetTag(string name)               => name != null && Tags.TryGetValue(name, out var t)      ? t : null;
        }

        private static AttributeDefinition Field(string id, EFieldType type) => new AttributeDefinition(id, type);

        private static ModifierDefinition Mod(string target, EModifierOperation op, float mag)
            => new ModifierDefinition(target, op, mag);

        private static CoreAttributeDefinition StrDef()
            => new CoreAttributeDefinition("str") { minValue = 0f, maxValue = 100f, defaultBase = 10f };

        // 组装：模板「平民」有 name/birthday 字段；特质「战士」带功能标签「战士标签」（有 rank 字段）+ str 修饰器。
        private static FakeSchema BuildSchema()
        {
            var schema = new FakeSchema();

            var template = new CharacterTemplate("平民");
            template.attributes.Add(Field(WellKnownAttr.Name, EFieldType.String));
            template.attributes.Add(Field(WellKnownAttr.Birthday, EFieldType.Int));
            schema.Templates["平民"] = template;

            var tag = new Tag("战士标签");
            tag.attributes.Add(Field("rank", EFieldType.Int));
            schema.Tags["战士标签"] = tag;

            var warrior = new TraitDefinition("战士") { functionTagRef = "战士标签" };
            warrior.modifiers.Add(Mod("str", EModifierOperation.Add, 5f));
            schema.Traits["战士"] = warrior;

            var blessed = new TraitDefinition("神眷");
            blessed.modifiers.Add(Mod("str", EModifierOperation.PercentAdd, 0.2f));
            schema.Traits["神眷"] = blessed;

            return schema;
        }

        [Test]
        public void RebuildAttributes_UnionOfTemplateAndTraitTagFields_InOrder()
        {
            var schema = BuildSchema();
            var ch = new CharacterDefinition("c1", "平民");
            ch.traits.Add(new CharacterTraitInstance("战士"));

            ch.RebuildAttributes(schema);

            // 模板字段在前（name, birthday），特质功能标签字段在后（rank）
            Assert.AreEqual(3, ch.values.Count);
            Assert.AreEqual(WellKnownAttr.Name,     ch.values[0].id);
            Assert.AreEqual(WellKnownAttr.Birthday, ch.values[1].id);
            Assert.AreEqual("rank",                 ch.values[2].id);
        }

        [Test]
        public void RebuildAttributes_RemovingTrait_DropsTagFields_KeepsTemplateValues()
        {
            var schema = BuildSchema();
            var ch = new CharacterDefinition("c1", "平民");
            ch.traits.Add(new CharacterTraitInstance("战士"));
            ch.RebuildAttributes(schema);

            ch.SetAttributeValue(WellKnownAttr.Name, "张三");   // 模板字段写值

            ch.traits.Clear();                                 // 卸下特质
            ch.RebuildAttributes(schema);

            Assert.IsNull(ch.GetEntry("rank"));                // 标签字段被移除
            Assert.AreEqual(2, ch.values.Count);
            Assert.AreEqual("张三", ch.GetAttributeValue<string>(WellKnownAttr.Name)); // 模板字段值保留
        }

        [Test]
        public void GetAge_IsWorldDayMinusBirthday_ClampedAtZero()
        {
            var schema = BuildSchema();
            var ch = new CharacterDefinition("c1", "平民");
            ch.RebuildAttributes(schema);
            ch.SetAttributeValue(WellKnownAttr.Birthday, 100);

            Assert.AreEqual(360, ch.GetAge(460));   // 460 − 100
            Assert.AreEqual(0,   ch.GetAge(50));    // 未出生 → 夹到 0
        }

        [Test]
        public void EndToEnd_CompositeEvaluation_TwoTraits_WithBreakdown()
        {
            var schema = BuildSchema();
            var ch = new CharacterDefinition("c1", "平民");
            ch.coreAttributes.Add(new CoreAttributeValue("str", 10f));
            ch.traits.Add(new CharacterTraitInstance("战士"));   // Add +5
            ch.traits.Add(new CharacterTraitInstance("神眷"));   // PercentAdd +0.2

            var e = CoreAttributeResolver.Evaluate(ch, StrDef(), schema);

            Assert.AreEqual(18f, e.Value, 1e-3f);   // (10 + 5) × 1.2
            var tags = new List<string>();
            foreach (var c in e.Breakdown) tags.Add(c.SourceTag);
            Assert.Contains("trait:战士", tags);
            Assert.Contains("trait:神眷", tags);
        }

        [Test]
        public void CompositeEvaluation_UsesCharacterBase_OverDefaultBase()
        {
            var schema = BuildSchema();
            var ch = new CharacterDefinition("c1", "平民");
            ch.coreAttributes.Add(new CoreAttributeValue("str", 20f));  // 覆盖 def.defaultBase(10)
            ch.traits.Add(new CharacterTraitInstance("战士"));           // Add +5

            var e = CoreAttributeResolver.Evaluate(ch, StrDef(), schema);
            Assert.AreEqual(25f, e.Value, 1e-3f);   // 20 + 5
        }

        [Test]
        public void GetCoreBaseValue_FallsBackToDefDefaultBase()
        {
            var ch = new CharacterDefinition("c1", "平民");   // 无 coreAttributes
            Assert.AreEqual(10f, ch.GetCoreBaseValue("str", StrDef()), 1e-4f);
            Assert.AreEqual(0f,  ch.GetCoreBaseValue("unknown"), 1e-4f);
        }

        [Test]
        public void Clone_IsDeep()
        {
            var schema = BuildSchema();
            var ch = new CharacterDefinition("c1", "平民");
            ch.traits.Add(new CharacterTraitInstance("战士"));
            ch.childRefs.Add("kid");
            ch.RebuildAttributes(schema);
            ch.SetAttributeValue(WellKnownAttr.Name, "原名");

            var clone = ch.Clone();
            clone.SetAttributeValue(WellKnownAttr.Name, "改名");
            clone.childRefs.Add("kid2");
            clone.traits.Clear();

            Assert.AreEqual("原名", ch.GetAttributeValue<string>(WellKnownAttr.Name)); // values 独立
            Assert.AreEqual(1, ch.childRefs.Count);                                    // 列表独立
            Assert.AreEqual(1, ch.traits.Count);                                       // 特质独立
        }
    }
}
