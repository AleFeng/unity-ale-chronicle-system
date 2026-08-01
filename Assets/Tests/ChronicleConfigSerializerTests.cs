using NUnit.Framework;
using UnityEngine;
using Ale.Chronicle;
using Ale.Chronicle.Serialization;
using Ale.Toolkit.Runtime;
using Ale.Condition;

namespace Ale.Chronicle.Tests
{
    /// <summary>
    /// 配置序列化门槛：ChronicleConfigSerializer 二进制往返后，枚举 / 标签 / 核心属性 / 特质（含 modifiers、
    /// 相性、AI 权重、eligibility 条件）/ 角色模板（含生成规则预留）/ 角色（含 values、核心属性、特质、家族指针）
    /// 各字段一致；Text/Sprite 字段类型正确。
    /// </summary>
    public class ChronicleConfigSerializerTests
    {
        private ChronicleDatabase _src, _dst;

        [TearDown]
        public void Cleanup()
        {
            if (_src != null) Object.DestroyImmediate(_src);
            if (_dst != null) Object.DestroyImmediate(_dst);
            _src = _dst = null;
        }

        private ChronicleDatabase BuildSource()
        {
            var db = ScriptableObject.CreateInstance<ChronicleDatabase>();

            db.AddEnumType("性别", "男", "女");

            // 属性模板 / 特质模板 / 分组标签 / 数字格式（v2 新列）
            var attrTmpl = new CoreAttributeTemplate("体格类") { minValue = 0f, maxValue = 100f, defaultBase = 10f };
            attrTmpl.attributes.Add(new AttributeDefinition("rarity", EFieldType.Int));
            db.CoreAttributeTemplates.Add(attrTmpl);

            var traitTmpl = new TraitTemplate("性格类");
            traitTmpl.attributes.Add(new AttributeDefinition("severity", EFieldType.Float));
            db.TraitTemplates.Add(traitTmpl);

            db.GroupTags.Add(new ChronicleGroupTag("g1", "分组一"));
            var nf = new NumberFormatConfig { name = "fmt" };
            var loc = new NumberFormatLocale { languageCode = "" };
            loc.rules.Add(new NumberFormatRule { threshold = 1000, divisor = 1000, decimalPlaces = 1 });
            nf.locales.Add(loc);
            db.NumberFormatConfigs.Add(nf);

            var tag = new Tag("战士标签");
            tag.displayNameText.SetTextValue(0, "战士");
            tag.attributes.Add(new AttributeDefinition("rank", EFieldType.Int));
            db.Tags.Add(tag);

            var str = new CoreAttributeDefinition("str") { templateRef = "体格类", minValue = 0f, maxValue = 100f, defaultBase = 10f, categoryEnumRef = "体格" };
            str.displayName.SetTextValue(0, "力量");
            db.CoreAttributes.Add(str);
            str.RebuildAttributes(db);            // 生成 rarity 自定义字段
            str.SetAttributeValue("rarity", 5);

            var brave = new TraitDefinition("brave")
            {
                templateRef = "性格类",
                lifetime = ETraitLifetime.Temporary,
                defaultDurationDays = 30f,
                durationStacksRefresh = true,
                categoryEnumRef = "性格",
                groupEquivalenceRef = "勇怯",
                functionTagRef = "战士标签",
                genetic = true,
                inheritChance = 0.5f,
                birthChance = 0.1f,
            };
            brave.displayName.SetTextValue(0, "勇敢");
            brave.modifiers.Add(new ModifierDefinition("str", EModifierOperation.Add, 5f, "custom"));
            brave.incompatibleTraitRefs.Add("craven");
            brave.compatibilities.Add(new TraitCompatibility("craven", -20f));
            brave.aiWeights.Add(new TraitAiWeight("boldness", 2f));
            var g = new ConditionGroup { itemOperator = ConditionLogicOp.And };
            var it = new ConditionItem("Chronicle.Age");
            var min = new ConditionParam("min", ConditionParamType.Int); min.SetInt(18);
            it.parameters.Add(min);
            g.items.Add(it);
            brave.eligibility.groups.Add(g);
            db.Traits.Add(brave);
            brave.RebuildAttributes(db);          // 生成 severity 自定义字段
            brave.SetAttributeValue("severity", 0.8f);

            var tmpl = new CharacterTemplate("平民") { raceRef = "human", attributePointBudget = 20, minAgeDays = 100 };
            tmpl.attributes.Add(new AttributeDefinition(WellKnownAttr.Name, EFieldType.String));
            tmpl.attributes.Add(new AttributeDefinition(WellKnownAttr.Birthday, EFieldType.Int));
            tmpl.guaranteedTraitRefs.Add("brave");
            db.CharacterTemplates.Add(tmpl);

            var c = new CharacterDefinition("c1", "平民") { fatherRef = "dad" };
            c.childRefs.Add("kid");
            c.coreAttributes.Add(new CoreAttributeValue("str", 12f));
            c.traits.Add(new CharacterTraitInstance("brave", 30f, 1, "birth"));
            db.Characters.Add(c);
            c.RebuildAttributes(db);                       // 生成 name/birthday(模板) + rank(特质标签)
            c.SetAttributeValue(WellKnownAttr.Name, "张三");
            c.SetAttributeValue(WellKnownAttr.Birthday, 100);
            c.SetAttributeValue("rank", 3);

            return db;
        }

        [Test]
        public void BinaryRoundTrip_PreservesAllData()
        {
            _src = BuildSource();
            byte[] bytes = ChronicleConfigSerializer.Export(_src);
            _dst = ChronicleConfigSerializer.Import(bytes);

            // 枚举
            Assert.AreEqual(2, _dst.GetEnumType("性别").items.Count);

            // 标签
            var tag = _dst.GetTag("战士标签");
            Assert.IsNotNull(tag);
            Assert.AreEqual("战士", tag.displayNameText.GetTextValue(0));
            Assert.AreEqual(1, tag.attributes.Count);
            Assert.AreEqual("rank", tag.attributes[0].id);

            // 核心属性
            var str = _dst.GetCoreAttribute("str");
            Assert.IsNotNull(str);
            Assert.AreEqual("力量", str.displayName.GetTextValue(0));
            Assert.AreEqual(EFieldType.Text, str.displayName.Type);
            Assert.AreEqual(EFieldType.Sprite, str.icon.Type);
            Assert.AreEqual(100f, str.maxValue, 1e-4f);
            Assert.AreEqual(10f, str.defaultBase, 1e-4f);
            Assert.AreEqual("体格", str.categoryEnumRef);
            Assert.AreEqual("体格类", str.templateRef);                    // v2
            Assert.AreEqual(5, str.GetAttributeValue<int>("rarity"));     // v2 values

            // 特质
            var brave = _dst.GetTrait("brave");
            Assert.IsNotNull(brave);
            Assert.AreEqual(ETraitLifetime.Temporary, brave.lifetime);
            Assert.AreEqual(30f, brave.defaultDurationDays, 1e-4f);
            Assert.AreEqual("战士标签", brave.functionTagRef);
            Assert.IsTrue(brave.genetic);
            Assert.AreEqual(0.5f, brave.inheritChance, 1e-4f);
            Assert.AreEqual(1, brave.modifiers.Count);
            Assert.AreEqual("str", brave.modifiers[0].targetAttributeId);
            Assert.AreEqual(EModifierOperation.Add, brave.modifiers[0].operation);
            Assert.AreEqual(5f, brave.modifiers[0].magnitude, 1e-4f);
            Assert.AreEqual("custom", brave.modifiers[0].sourceTag);
            Assert.Contains("craven", brave.incompatibleTraitRefs);
            Assert.AreEqual(1, brave.compatibilities.Count);
            Assert.AreEqual("craven", brave.compatibilities[0].otherTraitRef);
            Assert.AreEqual(-20f, brave.compatibilities[0].opinionDelta, 1e-4f);
            Assert.AreEqual("boldness", brave.aiWeights[0].axisRef);
            Assert.AreEqual("性格类", brave.templateRef);                            // v2
            Assert.AreEqual(0.8f, brave.GetAttributeValue<float>("severity"), 1e-3f); // v2 values

            // eligibility 条件往返
            Assert.AreEqual(1, brave.eligibility.TotalItemCount());
            Assert.AreEqual("Chronicle.Age", brave.eligibility.groups[0].items[0].key);
            Assert.AreEqual(18L, brave.eligibility.groups[0].items[0].parameters.Find("min").GetInt());

            // 角色模板（含生成规则预留）
            var tmpl = _dst.GetCharacterTemplate("平民");
            Assert.IsNotNull(tmpl);
            Assert.AreEqual(2, tmpl.attributes.Count);
            Assert.AreEqual("human", tmpl.raceRef);
            Assert.AreEqual(20, tmpl.attributePointBudget);
            Assert.AreEqual(100, tmpl.minAgeDays);
            Assert.Contains("brave", tmpl.guaranteedTraitRefs);

            // 角色
            var c = _dst.GetCharacter("c1");
            Assert.IsNotNull(c);
            Assert.AreEqual("平民", c.templateRef);
            Assert.AreEqual("dad", c.fatherRef);
            Assert.Contains("kid", c.childRefs);
            Assert.AreEqual("张三", c.GetAttributeValue<string>(WellKnownAttr.Name));
            Assert.AreEqual(100, c.GetAttributeValue<int>(WellKnownAttr.Birthday));
            Assert.AreEqual(3, c.GetAttributeValue<int>("rank"));
            Assert.AreEqual(1, c.coreAttributes.Count);
            Assert.AreEqual("str", c.coreAttributes[0].attrId);
            Assert.AreEqual(12f, c.coreAttributes[0].baseValue, 1e-4f);
            Assert.AreEqual(1, c.traits.Count);
            Assert.AreEqual("brave", c.traits[0].traitRef);
            Assert.AreEqual(30f, c.traits[0].remainingDays, 1e-4f);
            Assert.AreEqual("birth", c.traits[0].sourceTag);

            // v2 新列：属性模板 / 特质模板 / 分组标签 / 数字格式
            var at = _dst.GetCoreAttributeTemplate("体格类");
            Assert.IsNotNull(at);
            Assert.AreEqual(1, at.attributes.Count);
            Assert.AreEqual("rarity", at.attributes[0].id);
            Assert.IsNotNull(_dst.GetTraitTemplate("性格类"));
            Assert.IsNotNull(_dst.GetGroupTag("g1"));
            var fmt = _dst.GetNumberFormatConfig("fmt");
            Assert.IsNotNull(fmt);
            Assert.AreEqual(1000, fmt.locales[0].rules[0].threshold);
        }
    }
}
