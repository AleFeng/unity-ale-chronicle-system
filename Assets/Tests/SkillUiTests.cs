using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ale.Chronicle;
using Ale.Chronicle.Runtime.UI;

namespace Ale.Chronicle.Tests
{
    /// <summary>
    /// 技能 UI 采集器门槛（S4）：目录来源 = 全部已注册技能；角色已学来源 = SkillRuntimeManager 按学习顺序解析；
    /// 无角色 ID / 未知角色返回空。
    /// </summary>
    public class SkillUiTests
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
            ChronicleDataManager.Instance.ClearDatabases();   // 防止跨测试串数据
            SkillRuntimeManager.Instance.ResetAll();          // 清空已学技能
            foreach (var db in _created)
                if (db != null) Object.DestroyImmediate(db);
            _created.Clear();
        }

        [Test]
        public void Collect_Database_ReturnsAllCatalogSkills()
        {
            var db = NewDb();
            db.Skills.Add(new Skill("a"));
            db.Skills.Add(new Skill("b"));
            ChronicleDataManager.Instance.Register(db);

            var list = SkillCollector.Collect(ESkillSource.Database, null);
            CollectionAssert.AreEqual(new[] { "a", "b" }, list.ConvertAll(s => s.id));
        }

        [Test]
        public void Collect_Character_ReturnsLearnedInLearnOrder()
        {
            var db = NewDb();
            db.Skills.Add(new Skill("a"));
            db.Skills.Add(new Skill("b"));
            db.Skills.Add(new Skill("c"));
            ChronicleDataManager.Instance.Register(db);

            var mgr = SkillRuntimeManager.Instance;
            mgr.Learn("hero", "c");
            mgr.Learn("hero", "a");   // 未学 b

            var list = SkillCollector.Collect(ESkillSource.Character, "hero");
            CollectionAssert.AreEqual(new[] { "c", "a" }, list.ConvertAll(s => s.id));   // 学习顺序，未学不出现
        }

        [Test]
        public void Collect_Character_EmptyForNullOrUnknownCharacter()
        {
            var db = NewDb();
            db.Skills.Add(new Skill("a"));
            ChronicleDataManager.Instance.Register(db);

            Assert.AreEqual(0, SkillCollector.Collect(ESkillSource.Character, null).Count);
            Assert.AreEqual(0, SkillCollector.Collect(ESkillSource.Character, "nobody").Count);
        }
    }
}
