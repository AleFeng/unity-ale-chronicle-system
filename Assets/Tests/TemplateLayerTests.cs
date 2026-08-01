using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ale.Chronicle;
using Ale.Toolkit.Runtime;

namespace Ale.Chronicle.Tests
{
    /// <summary>
    /// 模板层门槛：属性/特质 实例按模板 schema RebuildAttributes；DB 新列（属性模板/特质模板/分组标签/数字格式）
    /// 的重复与悬空 templateRef 校验；CloneFrom 深拷贝新列；模板 Clone 深拷贝。
    /// </summary>
    public class TemplateLayerTests
    {
        private readonly List<ChronicleDatabase> _created = new List<ChronicleDatabase>();

        private ChronicleDatabase NewDb()
        {
            var db = ScriptableObject.CreateInstance<ChronicleDatabase>();
            _created.Add(db);
            return db;
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var db in _created)
                if (db != null) Object.DestroyImmediate(db);
            _created.Clear();
        }

        [Test]
        public void CoreAttribute_RebuildAttributes_FromTemplateSchema()
        {
            var db = NewDb();
            var tmpl = new CoreAttributeTemplate("体格") { minValue = 0f, maxValue = 200f };
            tmpl.attributes.Add(new AttributeDefinition("rarity", EFieldType.Int));
            db.CoreAttributeTemplates.Add(tmpl);

            var attr = new CoreAttributeDefinition("str") { templateRef = "体格" };
            db.CoreAttributes.Add(attr);

            attr.RebuildAttributes(db);
            Assert.AreEqual(1, attr.values.Count);
            Assert.IsNotNull(attr.GetEntry("rarity"));

            // 卸下模板 → 字段回收
            attr.templateRef = "";
            attr.RebuildAttributes(db);
            Assert.AreEqual(0, attr.values.Count);
        }

        [Test]
        public void Trait_RebuildAttributes_FromTemplateSchema()
        {
            var db = NewDb();
            var tmpl = new TraitTemplate("性格") { lifetime = ETraitLifetime.Permanent };
            tmpl.attributes.Add(new AttributeDefinition("severity", EFieldType.Float));
            db.TraitTemplates.Add(tmpl);

            var trait = new TraitDefinition("brave") { templateRef = "性格" };
            db.Traits.Add(trait);

            trait.RebuildAttributes(db);
            Assert.AreEqual(1, trait.values.Count);
            Assert.IsNotNull(trait.GetEntry("severity"));
        }

        [Test]
        public void Validate_DetectsDuplicateAndDanglingTemplateRefs()
        {
            var db = NewDb();
            db.CoreAttributeTemplates.Add(new CoreAttributeTemplate("体格"));
            db.CoreAttributeTemplates.Add(new CoreAttributeTemplate("体格"));   // 重复
            db.GroupTags.Add(new ChronicleGroupTag("g1"));
            db.GroupTags.Add(new ChronicleGroupTag("g1"));                       // 重复
            db.CoreAttributes.Add(new CoreAttributeDefinition("str") { templateRef = "缺失属性模板" });
            db.Traits.Add(new TraitDefinition("t") { templateRef = "缺失特质模板" });

            Assert.IsFalse(db.Validate(out var errors));
            Assert.IsTrue(errors.Exists(e => e.Contains("属性模板 name")));
            Assert.IsTrue(errors.Exists(e => e.Contains("分组标签 id")));
            Assert.IsTrue(errors.Exists(e => e.Contains("悬空")));
        }

        [Test]
        public void CloneFrom_IncludesNewLists_Deep()
        {
            var src = NewDb();
            src.CoreAttributeTemplates.Add(new CoreAttributeTemplate("体格") { maxValue = 200f });
            src.TraitTemplates.Add(new TraitTemplate("性格"));
            src.GroupTags.Add(new ChronicleGroupTag("g1"));
            src.NumberFormatConfigs.Add(new NumberFormatConfig { name = "fmt" });

            var dst = NewDb();
            dst.CloneFrom(src);

            Assert.AreEqual(1, dst.CoreAttributeTemplates.Count);
            Assert.AreEqual(1, dst.TraitTemplates.Count);
            Assert.AreEqual(1, dst.GroupTags.Count);
            Assert.AreEqual(1, dst.NumberFormatConfigs.Count);
            Assert.AreNotSame(src.GetCoreAttributeTemplate("体格"), dst.GetCoreAttributeTemplate("体格"));

            dst.GetCoreAttributeTemplate("体格").maxValue = 999f;
            Assert.AreEqual(200f, src.GetCoreAttributeTemplate("体格").maxValue, 1e-4f);
        }

        [Test]
        public void Template_Clone_IsDeep()
        {
            var t = new CoreAttributeTemplate("体格") { minValue = 1f };
            t.attributes.Add(new AttributeDefinition("rarity", EFieldType.Int));

            var c = t.Clone();
            c.attributes.Clear();
            c.minValue = 9f;

            Assert.AreEqual(1, t.attributes.Count);       // schema 独立
            Assert.AreEqual(1f, t.minValue, 1e-4f);       // 字段独立
        }
    }
}
