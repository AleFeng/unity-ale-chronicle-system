using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ale.Chronicle;
using Ale.Toolkit.Runtime;

namespace Ale.Chronicle.Tests
{
    /// <summary>
    /// 技能系统移植·核心门槛（S1）：Skill 依模板 schema 对账自定义字段（增补 / 移除 / 类型漂移）、
    /// Validate 抓技能重复 id / 技能模板重复 name、技能悬空引用（模板 / 分组标签，分组标签复用统一 groupTags 池）、
    /// CloneFrom 深拷贝技能与技能模板独立。序列化往返在 <see cref="ChronicleConfigSerializerTests"/> 覆盖。
    /// </summary>
    public class SkillSystemTests
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
        public void RebuildAttributes_ReconcilesToTemplateSchema()
        {
            var db = NewDb();
            var tmpl = new SkillTemplate("火系");
            tmpl.attributes.Add(new AttributeDefinition("power", EFieldType.Int));
            db.SkillTemplates.Add(tmpl);

            var s = new Skill("fireball", "火系");
            db.Skills.Add(s);

            // 初次对账：模板字段被引入
            s.RebuildAttributes(db);   // db 作为 IChronicleSchemaSource
            Assert.IsNotNull(s.GetEntry("power"), "模板字段 power 应被引入");

            // 模板新增字段：再对账后追加
            tmpl.attributes.Add(new AttributeDefinition("cooldown", EFieldType.Float));
            s.RebuildAttributes(db);
            Assert.IsNotNull(s.GetEntry("power"),    "已存在字段应保留");
            Assert.IsNotNull(s.GetEntry("cooldown"), "模板新增字段 cooldown 应被追加");

            // 模板移除字段：再对账后剔除
            tmpl.attributes.Clear();
            tmpl.attributes.Add(new AttributeDefinition("cooldown", EFieldType.Float));
            s.RebuildAttributes(db);
            Assert.IsNull(s.GetEntry("power"),       "模板已移除的字段 power 应被剔除");
            Assert.IsNotNull(s.GetEntry("cooldown"));
        }

        [Test]
        public void Validate_CleanSkills_Passes()
        {
            var db = NewDb();
            db.SkillTemplates.Add(new SkillTemplate("火系"));
            db.GroupTags.Add(new ChronicleGroupTag("g1", "攻击"));

            var s = new Skill("fireball", "火系") { primaryGroupTag = "g1" };
            s.secondaryGroupTags.Add("g1");
            db.Skills.Add(s);

            Assert.IsTrue(db.Validate(out var errors), string.Join(" | ", errors));
            Assert.AreEqual(0, errors.Count);
        }

        [Test]
        public void Validate_DetectsDuplicateSkillIdsAndTemplateNames()
        {
            var db = NewDb();
            db.Skills.Add(new Skill("s1"));
            db.Skills.Add(new Skill("s1"));                 // 重复 id
            db.SkillTemplates.Add(new SkillTemplate("火系"));
            db.SkillTemplates.Add(new SkillTemplate("火系")); // 重复 name

            Assert.IsFalse(db.Validate(out var errors));
            Assert.IsTrue(errors.Exists(e => e.Contains("技能 id")));
            Assert.IsTrue(errors.Exists(e => e.Contains("技能模板 name")));
        }

        [Test]
        public void Validate_DetectsDanglingSkillRefs()
        {
            var db = NewDb();
            // 引用不存在的模板 + 不存在的分组标签（主 / 副）
            var s = new Skill("s1", "缺失模板") { primaryGroupTag = "缺失主标签" };
            s.secondaryGroupTags.Add("缺失副标签");
            db.Skills.Add(s);

            Assert.IsFalse(db.Validate(out var errors));
            string all = string.Join(" | ", errors);
            Assert.IsTrue(all.Contains("悬空"));
            Assert.IsTrue(all.Contains("缺失模板"));
            Assert.IsTrue(all.Contains("缺失主标签"));
            Assert.IsTrue(all.Contains("缺失副标签"));
        }

        [Test]
        public void CloneFrom_DeepCopiesSkills()
        {
            var src = NewDb();
            var tmpl = new SkillTemplate("火系");
            tmpl.attributes.Add(new AttributeDefinition("power", EFieldType.Int));
            src.SkillTemplates.Add(tmpl);

            var s = new Skill("fireball", "火系");
            src.Skills.Add(s);
            s.RebuildAttributes(src);
            s.SetAttributeValue("power", 30);

            var dst = NewDb();
            dst.CloneFrom(src);

            Assert.AreEqual(1, dst.SkillTemplates.Count);
            Assert.AreEqual(1, dst.Skills.Count);
            Assert.AreNotSame(src.GetSkill("fireball"), dst.GetSkill("fireball"));   // 不同实例
            Assert.AreEqual(30, dst.GetSkill("fireball").GetAttributeValue<int>("power"));

            dst.GetSkill("fireball").SetAttributeValue("power", 999);                // 改克隆
            Assert.AreEqual(30, src.GetSkill("fireball").GetAttributeValue<int>("power")); // 源不受影响
        }
    }
}
