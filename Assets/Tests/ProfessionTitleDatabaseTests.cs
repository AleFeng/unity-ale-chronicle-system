using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ale.Chronicle;
using Ale.Toolkit.Runtime;

namespace Ale.Chronicle.Tests
{
    /// <summary>
    /// 职业/头衔 Step 3 数据库接线门槛：4 个 finder；Validate 对干净库通过、抓悬空成长目标 /
    /// 转职树成环 / 阶级序列含非阶级头衔 / 重复职业 id；CloneFrom 深拷贝新增 4 列表。
    /// </summary>
    public class ProfessionTitleDatabaseTests
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

        // 引用自洽的干净库：str 属性；warrior 职业(成长→str)；duke 阶级头衔(修饰器→str)；一棵单节点树；一条序列。
        private ChronicleDatabase CleanDb()
        {
            var db = NewDb();
            db.CoreAttributes.Add(new CoreAttributeDefinition("str"));

            var p = new ProfessionDefinition("warrior");
            p.growth.Add(new LevelGrowthEntry { coreAttrId = "str", perLevel = 2f });
            db.Professions.Add(p);

            var t = new TitleDefinition("duke") { kind = ETitleKind.RankTitle };
            t.modifiers.Add(new ModifierDefinition("str", EModifierOperation.Add, 5f));
            db.Titles.Add(t);

            var tree = new ProfessionTree("warrior_line");
            tree.nodes.Add(new ProfessionTreeNode { professionRef = "warrior" });
            db.ProfessionTrees.Add(tree);

            var l = new RankLadder("peerage");
            l.orderedTitleRefs.Add("duke");
            db.RankLadders.Add(l);
            return db;
        }

        [Test]
        public void Finders_ResolveByIdOrNull()
        {
            var db = CleanDb();
            Assert.IsNotNull(db.GetProfession("warrior"));
            Assert.IsNotNull(db.GetTitle("duke"));
            Assert.IsNotNull(db.GetProfessionTree("warrior_line"));
            Assert.IsNotNull(db.GetRankLadder("peerage"));
            Assert.IsNull(db.GetProfession("nope"));
            Assert.IsNull(db.GetTitle("nope"));
        }

        [Test]
        public void Validate_CleanDb_Passes()
        {
            var db = CleanDb();
            Assert.IsTrue(db.Validate(out var errors), string.Join(" | ", errors));
        }

        [Test]
        public void Validate_DetectsDanglingGrowthTarget()
        {
            var db = NewDb();
            var p = new ProfessionDefinition("warrior");
            p.growth.Add(new LevelGrowthEntry { coreAttrId = "缺失属性", perLevel = 2f });
            db.Professions.Add(p);

            Assert.IsFalse(db.Validate(out var errors));
            Assert.IsTrue(errors.Exists(e => e.Contains("悬空")));
        }

        [Test]
        public void Validate_DetectsProfessionTreeCycle()
        {
            var db = NewDb();
            db.Professions.Add(new ProfessionDefinition("warrior"));
            db.Professions.Add(new ProfessionDefinition("knight"));

            var tree = new ProfessionTree("t");
            tree.nodes.Add(new ProfessionTreeNode { professionRef = "warrior", childProfessionRefs = { "knight" } });
            tree.nodes.Add(new ProfessionTreeNode { professionRef = "knight", childProfessionRefs = { "warrior" } });
            db.ProfessionTrees.Add(tree);

            Assert.IsFalse(db.Validate(out var errors));
            Assert.IsTrue(errors.Exists(e => e.Contains("环")));
        }

        [Test]
        public void Validate_DetectsNonRankTitleInLadder()
        {
            var db = NewDb();
            db.Titles.Add(new TitleDefinition("hero") { kind = ETitleKind.Epithet }); // 称号不应入阶梯
            var l = new RankLadder("l");
            l.orderedTitleRefs.Add("hero");
            db.RankLadders.Add(l);

            Assert.IsFalse(db.Validate(out var errors));
            Assert.IsTrue(errors.Exists(e => e.Contains("非阶级头衔")));
        }

        [Test]
        public void Validate_DetectsDuplicateProfessionId()
        {
            var db = NewDb();
            db.Professions.Add(new ProfessionDefinition("warrior"));
            db.Professions.Add(new ProfessionDefinition("warrior"));

            Assert.IsFalse(db.Validate(out var errors));
            Assert.IsTrue(errors.Exists(e => e.Contains("职业 id")));
        }

        [Test]
        public void CloneFrom_IncludesNewLists_Deep()
        {
            var src = CleanDb();
            var dst = NewDb();
            dst.CloneFrom(src);

            Assert.AreEqual(1, dst.Professions.Count);
            Assert.AreEqual(1, dst.Titles.Count);
            Assert.AreEqual(1, dst.ProfessionTrees.Count);
            Assert.AreEqual(1, dst.RankLadders.Count);
            Assert.AreNotSame(src.GetProfession("warrior"), dst.GetProfession("warrior")); // 不同实例

            dst.GetProfession("warrior").maxLevel = 999;
            Assert.AreEqual(10, src.GetProfession("warrior").maxLevel); // 源不受影响（默认 maxLevel=10）
        }
    }
}
