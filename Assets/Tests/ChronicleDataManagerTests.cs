using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ale.Chronicle;

namespace Ale.Chronicle.Tests
{
    /// <summary>
    /// 运行时数据管理器门槛：跨库 O(1) 查询；「先注册的数据库优先」；注销 / 清空后索引不残留旧数据；
    /// LoadFromBinary 导入并注册。
    /// </summary>
    public class ChronicleDataManagerTests
    {
        private readonly List<ChronicleDatabase> _created = new List<ChronicleDatabase>();

        private ChronicleDatabase NewDbWithTrait(string traitId, string displayName)
        {
            var db = ScriptableObject.CreateInstance<ChronicleDatabase>();
            var t = new TraitDefinition(traitId);
            t.displayName.SetTextValue(0, displayName);
            db.Traits.Add(t);
            _created.Add(db);
            return db;
        }

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
            foreach (var db in _created)
                if (db != null) Object.DestroyImmediate(db);
            _created.Clear();
        }

        [Test]
        public void FirstRegisteredDatabaseWins()
        {
            var db1 = NewDbWithTrait("x", "db1");
            var db2 = NewDbWithTrait("x", "db2");

            var mgr = ChronicleDataManager.Instance;
            mgr.Register(db1);
            mgr.Register(db2);

            Assert.AreSame(db1.GetTrait("x"), mgr.GetTrait("x"));           // 先注册者命中
            Assert.AreEqual("db1", mgr.GetTrait("x").displayName.GetTextValue(0));
        }

        [Test]
        public void ClearDatabases_LeavesNoStaleData()
        {
            var db = NewDbWithTrait("x", "db");
            var mgr = ChronicleDataManager.Instance;
            mgr.Register(db);
            Assert.IsNotNull(mgr.GetTrait("x"));

            mgr.ClearDatabases();
            Assert.IsNull(mgr.GetTrait("x"));   // 索引重建后无残留
        }

        [Test]
        public void Unregister_RemovesEntries()
        {
            var db = NewDbWithTrait("x", "db");
            var mgr = ChronicleDataManager.Instance;
            mgr.Register(db);
            Assert.IsNotNull(mgr.GetTrait("x"));

            mgr.Unregister(db);
            Assert.IsNull(mgr.GetTrait("x"));
        }

        // ── 技能 UI 迁移所需的目录 / 分组标签访问器（S1）────────────────────────────

        [Test]
        public void GetAllSkills_AggregatesAcrossDatabases_DedupByIdFirstWins()
        {
            var db1 = NewDb();
            db1.Skills.Add(new Skill("a"));
            db1.Skills.Add(new Skill("b"));
            var db2 = NewDb();
            db2.Skills.Add(new Skill("b"));   // 重复 id：应被 db1 的 b 覆盖（先注册先得）
            db2.Skills.Add(new Skill("c"));

            var mgr = ChronicleDataManager.Instance;
            mgr.Register(db1);
            mgr.Register(db2);

            var all = mgr.GetAllSkills();
            Assert.AreEqual(3, all.Count, "id 去重后应为 a / b / c 三个");
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, all.ConvertAll(s => s.id), "按注册 / 列表顺序保序");
            Assert.AreSame(db1.GetSkill("b"), mgr.GetSkill("b"), "重复 id 命中先注册者");
        }

        [Test]
        public void GetGroupTag_And_GetAllGroupTags_Work()
        {
            var db = NewDb();
            db.GroupTags.Add(new ChronicleGroupTag("g1", "攻击"));
            db.GroupTags.Add(new ChronicleGroupTag("g2", "防御"));

            var mgr = ChronicleDataManager.Instance;
            mgr.Register(db);

            Assert.AreSame(db.GetGroupTag("g1"), mgr.GetGroupTag("g1"));
            Assert.IsNull(mgr.GetGroupTag("missing"));

            var all = mgr.GetAllGroupTags();
            Assert.AreEqual(2, all.Count);
            CollectionAssert.AreEqual(new[] { "g1", "g2" }, all.ConvertAll(t => t.id));
        }

        [Test]
        public void GetAll_ReturnEmptyNotNull_WhenNoDatabases()
        {
            var mgr = ChronicleDataManager.Instance;
            Assert.IsNotNull(mgr.GetAllSkills());
            Assert.AreEqual(0, mgr.GetAllSkills().Count);
            Assert.IsNotNull(mgr.GetAllGroupTags());
            Assert.AreEqual(0, mgr.GetAllGroupTags().Count);
        }

        // ── 职业 / 头衔索引（S5）────────────────────────────────────────────────────

        [Test]
        public void GetProfessionAndTitle_ResolveById()
        {
            var db = NewDb();
            db.Professions.Add(new ProfessionDefinition("warrior"));
            db.Titles.Add(new TitleDefinition("duke"));
            db.ProfessionTrees.Add(new ProfessionTree("warrior_line"));
            db.RankLadders.Add(new RankLadder("peerage"));

            var mgr = ChronicleDataManager.Instance;
            mgr.Register(db);

            Assert.AreSame(db.GetProfession("warrior"), mgr.GetProfession("warrior"));
            Assert.AreSame(db.GetTitle("duke"), mgr.GetTitle("duke"));
            Assert.AreSame(db.GetProfessionTree("warrior_line"), mgr.GetProfessionTree("warrior_line"));
            Assert.AreSame(db.GetRankLadder("peerage"), mgr.GetRankLadder("peerage"));
            Assert.IsNull(mgr.GetProfession("missing"));
        }

        [Test]
        public void GetAllProfessions_AggregatesAcrossDatabases_DedupByIdFirstWins()
        {
            var db1 = NewDb();
            db1.Professions.Add(new ProfessionDefinition("a"));
            db1.Titles.Add(new TitleDefinition("t1"));
            db1.RankLadders.Add(new RankLadder("l1"));
            var db2 = NewDb();
            db2.Professions.Add(new ProfessionDefinition("a"));   // 重复 id → 先注册先得
            db2.Professions.Add(new ProfessionDefinition("b"));

            var mgr = ChronicleDataManager.Instance;
            mgr.Register(db1);
            mgr.Register(db2);

            var profs = mgr.GetAllProfessions();
            Assert.AreEqual(2, profs.Count);
            CollectionAssert.AreEqual(new[] { "a", "b" }, profs.ConvertAll(p => p.id));
            Assert.AreSame(db1.GetProfession("a"), mgr.GetProfession("a"));
            Assert.AreEqual(1, mgr.GetAllTitles().Count);
            Assert.AreEqual(1, mgr.GetAllRankLadders().Count);
        }

        [Test]
        public void GetAllProfessionsTitles_EmptyNotNull_WhenNoDatabases()
        {
            var mgr = ChronicleDataManager.Instance;
            Assert.IsNotNull(mgr.GetAllProfessions());
            Assert.AreEqual(0, mgr.GetAllProfessions().Count);
            Assert.IsNotNull(mgr.GetAllTitles());
            Assert.AreEqual(0, mgr.GetAllRankLadders().Count);
        }
    }
}
