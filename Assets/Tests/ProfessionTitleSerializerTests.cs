using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using Ale.Chronicle;
using Ale.Chronicle.Serialization;
using Ale.Toolkit.Runtime;
using Ale.Condition;

namespace Ale.Chronicle.Tests
{
    /// <summary>
    /// 职业/头衔 Step 4 序列化门槛：v4 二进制往返保住 职业（曲线经验/成长/解锁/requirements 条件）、
    /// 转职树边、头衔（两 kind/修饰器/好感修饰器/acquisition 条件）、阶级序列、角色持有的职业/头衔；
    /// 且旧 v3 文件仍可导入（职业/头衔为空、无异常）。
    /// </summary>
    public class ProfessionTitleSerializerTests
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
            db.CoreAttributes.Add(new CoreAttributeDefinition("str"));
            db.GroupTags.Add(new ChronicleGroupTag("g1", "分组一"));

            // 职业 warrior：Curve 经验曲线 + 成长 + 解锁 + requirements(Age>=18)
            var warrior = new ProfessionDefinition("warrior") { groupTagRef = "g1", maxLevel = 20 };
            warrior.displayName.SetTextValue(0, "战士");
            warrior.expCurve.mode = EExpCurveMode.Curve;
            warrior.expCurve.curveScale = 2f;
            warrior.expCurve.curveValue.SetAnimationCurve(0, AnimationCurve.Linear(1f, 100f, 10f, 1000f));
            warrior.growth.Add(new LevelGrowthEntry { coreAttrId = "str", perLevel = 2f });
            warrior.unlocks.Add(new LevelUnlock { level = 3, grantTraitRefs = { "brave" }, grantTitleRefs = { "duke" } });
            warrior.allowedRaceRefs.Add("human");
            var g = new ConditionGroup { itemOperator = ConditionLogicOp.And };
            var it = new ConditionItem("Chronicle.Age");
            var min = new ConditionParam("min", ConditionParamType.Int); min.SetInt(18);
            it.parameters.Add(min);
            g.items.Add(it);
            warrior.requirements.groups.Add(g);
            db.Professions.Add(warrior);

            db.Professions.Add(new ProfessionDefinition("knight"));

            // 转职树 warrior → knight
            var tree = new ProfessionTree("warrior_line");
            tree.displayName.SetTextValue(0, "战士线");
            tree.nodes.Add(new ProfessionTreeNode { professionRef = "warrior", childProfessionRefs = { "knight" } });
            tree.nodes.Add(new ProfessionTreeNode { professionRef = "knight" });
            db.ProfessionTrees.Add(tree);

            // 头衔 duke（阶级头衔）+ hero（称号）
            var duke = new TitleDefinition("duke")
            {
                kind = ETitleKind.RankTitle, rankTier = 5, heritable = true, isUnique = true, isRevocable = false,
                groupTagRef = "g1", successionPolicyRef = "primogeniture",
            };
            duke.displayName.SetTextValue(0, "公爵");
            duke.modifiers.Add(new ModifierDefinition("str", EModifierOperation.Add, 3f, "title:duke"));
            duke.opinionModifiers.Add(new ModifierDefinition("威望", EModifierOperation.Add, 10f, null));
            var g2 = new ConditionGroup { itemOperator = ConditionLogicOp.And };
            var it2 = new ConditionItem("Chronicle.Age");
            var min2 = new ConditionParam("min", ConditionParamType.Int); min2.SetInt(30);
            it2.parameters.Add(min2);
            g2.items.Add(it2);
            duke.acquisitionConditions.groups.Add(g2);
            db.Titles.Add(duke);

            db.Titles.Add(new TitleDefinition("hero") { kind = ETitleKind.Epithet });

            // 阶级序列 peerage：[duke]
            var ladder = new RankLadder("peerage");
            ladder.orderedTitleRefs.Add("duke");
            db.RankLadders.Add(ladder);

            // 角色 c1 持有 职业 + 头衔
            var c = new CharacterDefinition("c1", null);
            c.professions.Add(new CharacterProfession("warrior", 5, 10, true));
            c.titles.Add(new CharacterTitle("duke", 100));
            db.Characters.Add(c);

            return db;
        }

        [Test]
        public void BinaryRoundTrip_V4_PreservesProfessionAndTitle()
        {
            _src = BuildSource();
            byte[] bytes = ChronicleConfigSerializer.Export(_src);
            _dst = ChronicleConfigSerializer.Import(bytes);

            // 职业
            var w = _dst.GetProfession("warrior");
            Assert.IsNotNull(w);
            Assert.AreEqual("战士", w.displayName.GetTextValue(0));
            Assert.AreEqual("g1", w.groupTagRef);
            Assert.AreEqual(20, w.maxLevel);
            Assert.AreEqual(EExpCurveMode.Curve, w.expCurve.mode);
            Assert.AreEqual(200, w.expCurve.ExpToNext(1));   // 曲线 Evaluate(1)=100 × scale 2
            Assert.AreEqual(1, w.growth.Count);
            Assert.AreEqual("str", w.growth[0].coreAttrId);
            Assert.AreEqual(2f, w.growth[0].perLevel, 1e-4f);
            Assert.AreEqual(1, w.unlocks.Count);
            Assert.AreEqual(3, w.unlocks[0].level);
            Assert.Contains("brave", w.unlocks[0].grantTraitRefs);
            Assert.Contains("duke", w.unlocks[0].grantTitleRefs);
            Assert.Contains("human", w.allowedRaceRefs);
            Assert.AreEqual(1, w.requirements.TotalItemCount());
            Assert.AreEqual("Chronicle.Age", w.requirements.groups[0].items[0].key);

            // 转职树
            var tree = _dst.GetProfessionTree("warrior_line");
            Assert.IsNotNull(tree);
            Assert.AreEqual("战士线", tree.displayName.GetTextValue(0));
            var node = tree.FindNode("warrior");
            Assert.IsNotNull(node);
            Assert.Contains("knight", node.childProfessionRefs);

            // 头衔
            var duke = _dst.GetTitle("duke");
            Assert.IsNotNull(duke);
            Assert.AreEqual("公爵", duke.displayName.GetTextValue(0));
            Assert.AreEqual(ETitleKind.RankTitle, duke.kind);
            Assert.AreEqual(5, duke.rankTier);
            Assert.IsTrue(duke.heritable);
            Assert.IsTrue(duke.isUnique);
            Assert.IsFalse(duke.isRevocable);
            Assert.AreEqual("primogeniture", duke.successionPolicyRef);
            Assert.AreEqual(1, duke.modifiers.Count);
            Assert.AreEqual("str", duke.modifiers[0].targetAttributeId);
            Assert.AreEqual(3f, duke.modifiers[0].magnitude, 1e-4f);
            Assert.AreEqual(1, duke.opinionModifiers.Count);
            Assert.AreEqual(1, duke.acquisitionConditions.TotalItemCount());
            Assert.AreEqual(ETitleKind.Epithet, _dst.GetTitle("hero").kind);

            // 阶级序列
            var ladder = _dst.GetRankLadder("peerage");
            Assert.IsNotNull(ladder);
            Assert.Contains("duke", ladder.orderedTitleRefs);

            // 角色持有
            var c = _dst.GetCharacter("c1");
            Assert.IsNotNull(c);
            Assert.AreEqual(1, c.professions.Count);
            Assert.AreEqual("warrior", c.professions[0].professionRef);
            Assert.AreEqual(5, c.professions[0].level);
            Assert.AreEqual(10, c.professions[0].currentExp);
            Assert.IsTrue(c.professions[0].isPrimary);
            Assert.AreEqual(1, c.titles.Count);
            Assert.AreEqual("duke", c.titles[0].titleRef);
            Assert.AreEqual(100, c.titles[0].acquiredWorldDay);
        }

        [Test]
        public void OldV3File_Loads_WithEmptyProfessionsAndTitles()
        {
            // 手工构造一个「全空 v3」字节流：魔数 + 版本3 + 12 个空块
            // （6 基础块 + 4 个 v2 块 + 2 个 v3 块，各写 count=0）。v4 读端应跳过 v4 块。
            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                using (var w = new BinaryWriter(ms, Encoding.UTF8))
                {
                    w.Write(0x4348524F);                       // "CHRO"
                    w.Write(3);                                // version 3
                    for (int i = 0; i < 12; i++) w.Write(0);   // 12 个空数组
                }
                bytes = ms.ToArray();
            }

            _dst = ChronicleConfigSerializer.Import(bytes);
            Assert.IsNotNull(_dst);
            Assert.AreEqual(0, _dst.Characters.Count);
            Assert.AreEqual(0, _dst.Professions.Count);
            Assert.AreEqual(0, _dst.ProfessionTrees.Count);
            Assert.AreEqual(0, _dst.Titles.Count);
            Assert.AreEqual(0, _dst.RankLadders.Count);
        }
    }
}
