using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ale.Chronicle;
using Ale.Toolkit.Runtime;

namespace Ale.Chronicle.Tests
{
    /// <summary>
    /// 中央数据库门槛：Validate 抓重复 id/name 与悬空引用；干净库通过；CloneFrom 深拷贝独立；
    /// 作为 IChronicleSchemaSource 驱动 RebuildAttributes；AddEnumType 幂等。
    /// </summary>
    public class ChronicleDatabaseTests
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

        // 一个引用自洽的干净库。
        private ChronicleDatabase CleanDb()
        {
            var db = NewDb();
            db.CoreAttributes.Add(new CoreAttributeDefinition("str"));
            db.Traits.Add(new TraitDefinition("brave"));
            db.CharacterTemplates.Add(new CharacterTemplate("平民"));
            var c = new CharacterDefinition("c1", "平民");
            c.traits.Add(new CharacterTraitInstance("brave"));
            c.coreAttributes.Add(new CoreAttributeValue("str", 5f));
            db.Characters.Add(c);
            return db;
        }

        [Test]
        public void Validate_CleanDb_Passes()
        {
            var db = CleanDb();
            Assert.IsTrue(db.Validate(out var errors), string.Join(" | ", errors));
            Assert.AreEqual(0, errors.Count);
        }

        [Test]
        public void Validate_DetectsDuplicateIds()
        {
            var db = NewDb();
            db.CoreAttributes.Add(new CoreAttributeDefinition("str"));
            db.CoreAttributes.Add(new CoreAttributeDefinition("str"));   // 重复
            db.Traits.Add(new TraitDefinition("brave"));
            db.Traits.Add(new TraitDefinition("brave"));                 // 重复

            Assert.IsFalse(db.Validate(out var errors));
            Assert.IsTrue(errors.Exists(e => e.Contains("核心属性 id")));
            Assert.IsTrue(errors.Exists(e => e.Contains("特质 id")));
        }

        [Test]
        public void Validate_DetectsDanglingRefs()
        {
            var db = NewDb();

            // 特质 modifier 指向不存在的属性
            var t = new TraitDefinition("t");
            t.modifiers.Add(new ModifierDefinition("缺失属性", EModifierOperation.Add, 1f));
            db.Traits.Add(t);

            // 角色引用不存在的模板 / 特质 / 核心属性
            var c = new CharacterDefinition("c1", "缺失模板");
            c.traits.Add(new CharacterTraitInstance("缺失特质"));
            c.coreAttributes.Add(new CoreAttributeValue("缺失属性2", 1f));
            db.Characters.Add(c);

            Assert.IsFalse(db.Validate(out var errors));
            Assert.IsTrue(errors.Exists(e => e.Contains("悬空")));
        }

        [Test]
        public void CloneFrom_IsDeep()
        {
            var src = NewDb();
            src.CoreAttributes.Add(new CoreAttributeDefinition("str") { maxValue = 100f });
            src.Traits.Add(new TraitDefinition("brave"));

            var dst = NewDb();
            dst.CloneFrom(src);

            Assert.AreEqual(1, dst.CoreAttributes.Count);
            Assert.AreEqual(1, dst.Traits.Count);
            Assert.AreNotSame(src.GetCoreAttribute("str"), dst.GetCoreAttribute("str"));  // 不同实例

            dst.GetCoreAttribute("str").maxValue = 999f;                                   // 改克隆
            Assert.AreEqual(100f, src.GetCoreAttribute("str").maxValue, 1e-4f);            // 源不受影响
        }

        [Test]
        public void ActsAsSchemaSource_ForRebuildAttributes()
        {
            var db = NewDb();
            var tmpl = new CharacterTemplate("平民");
            tmpl.attributes.Add(new AttributeDefinition(WellKnownAttr.Name, EFieldType.String));
            db.CharacterTemplates.Add(tmpl);

            var c = new CharacterDefinition("c1", "平民");
            db.Characters.Add(c);

            c.RebuildAttributes(db);   // db 作为 IChronicleSchemaSource
            Assert.IsNotNull(c.GetEntry(WellKnownAttr.Name));
        }

        [Test]
        public void AddEnumType_IsIdempotent()
        {
            var db = NewDb();
            db.AddEnumType("性别", "男", "女");
            db.AddEnumType("性别", "无");           // 同名忽略

            Assert.AreEqual(1, db.EnumTypesList.Count);
            Assert.AreEqual(2, db.GetEnumType("性别").items.Count);
        }
    }
}
