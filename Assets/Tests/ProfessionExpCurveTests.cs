using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ale.Chronicle;
using Ale.Toolkit.Runtime;

namespace Ale.Chronicle.Tests
{
    /// <summary>
    /// 职业系统 Step 1 数据层门槛：ExpCurve 三模式求值一致 / 边界；LevelGrowthEntry 成长；
    /// ProfessionDefinition 的成长汇流（Add + prof:{id}:growth 来源标记）、显示名回退与深拷贝；ProfessionTree 根派生。
    /// </summary>
    public class ProfessionExpCurveTests
    {
        // ── ExpCurve ──────────────────────────────────────────────────────────────

        [Test]
        public void TotalExpForLevel_One_IsZero()
        {
            var formula = new ExpCurve { mode = EExpCurveMode.Formula, baseExp = 100f, exponent = 1.5f, linear = 10f };
            Assert.AreEqual(0, formula.TotalExpForLevel(1));
        }

        [Test]
        public void Formula_And_EquivalentTable_MatchTotals()
        {
            var formula = new ExpCurve { mode = EExpCurveMode.Formula, baseExp = 100f, exponent = 1.5f, linear = 10f };

            const int N = 8;
            var table = new ExpCurve { mode = EExpCurveMode.Table };
            for (int k = 1; k <= N; k++) table.perLevelExp.Add(formula.ExpToNext(k));

            for (int k = 1; k < N; k++)
                Assert.AreEqual(formula.ExpToNext(k), table.ExpToNext(k), "ExpToNext mismatch at level " + k);

            Assert.AreEqual(formula.TotalExpForLevel(N), table.TotalExpForLevel(N));
        }

        [Test]
        public void Table_ClampsBeyondEnd()
        {
            var t = new ExpCurve { mode = EExpCurveMode.Table };
            t.perLevelExp.AddRange(new[] { 10, 20, 30 });
            Assert.AreEqual(10, t.ExpToNext(1));
            Assert.AreEqual(30, t.ExpToNext(3));
            Assert.AreEqual(30, t.ExpToNext(7));   // 超表尾按末项
        }

        [Test]
        public void Table_Empty_ReturnsZero()
        {
            var t = new ExpCurve { mode = EExpCurveMode.Table };
            Assert.AreEqual(0, t.ExpToNext(3));
            Assert.AreEqual(0, t.TotalExpForLevel(5));
        }

        [Test]
        public void Curve_IsMonotonicWithScale()
        {
            var c = new ExpCurve { mode = EExpCurveMode.Curve, curveScale = 2f };
            c.curveValue.SetAnimationCurve(0, AnimationCurve.Linear(1f, 100f, 10f, 1000f));
            Assert.AreEqual(200, c.ExpToNext(1));    // 100 × 2
            Assert.AreEqual(2000, c.ExpToNext(10));  // 1000 × 2
            Assert.Less(c.ExpToNext(1), c.ExpToNext(10));
        }

        // ── LevelGrowthEntry ──────────────────────────────────────────────────────

        [Test]
        public void MagnitudeAt_LinearGrowth_AccruesFromLevelTwo()
        {
            var g = new LevelGrowthEntry { coreAttrId = "力量", perLevel = 2f };
            Assert.AreEqual(0f, g.MagnitudeAt(1), 1e-4f);   // 1 级刚转职成长为 0
            Assert.AreEqual(8f, g.MagnitudeAt(5), 1e-4f);   // 2 × (5-1)
        }

        // ── ProfessionDefinition 成长汇流 ─────────────────────────────────────────

        [Test]
        public void CollectGrowthModifiers_TargetFilter_AddWithSourceTag()
        {
            var prof = new ProfessionDefinition("warrior");
            prof.growth.Add(new LevelGrowthEntry { coreAttrId = "力量", perLevel = 2f });
            prof.growth.Add(new LevelGrowthEntry { coreAttrId = "敏捷", perLevel = 1f });

            var into = new List<ModifierDefinition>();
            prof.CollectGrowthModifiers(5, "力量", into);

            Assert.AreEqual(1, into.Count);
            Assert.AreEqual("力量", into[0].targetAttributeId);
            Assert.AreEqual(EModifierOperation.Add, into[0].operation);
            Assert.AreEqual(8f, into[0].magnitude, 1e-4f);
            Assert.AreEqual("prof:warrior:growth", into[0].sourceTag);
        }

        [Test]
        public void CollectGrowthModifiers_NullTarget_CollectsAll()
        {
            var prof = new ProfessionDefinition("warrior");
            prof.growth.Add(new LevelGrowthEntry { coreAttrId = "力量", perLevel = 2f });
            prof.growth.Add(new LevelGrowthEntry { coreAttrId = "敏捷", perLevel = 1f });

            var into = new List<ModifierDefinition>();
            prof.CollectGrowthModifiers(5, null, into);
            Assert.AreEqual(2, into.Count);
        }

        [Test]
        public void PlainName_FallsBackToId()
        {
            var p = new ProfessionDefinition("warrior");
            Assert.AreEqual("warrior", p.PlainName());
            p.displayName.SetTextValue(0, "战士");
            Assert.AreEqual("战士", p.PlainName());
        }

        [Test]
        public void Clone_IsDeep()
        {
            var p = new ProfessionDefinition("warrior") { maxLevel = 20 };
            p.displayName.SetTextValue(0, "战士");
            p.growth.Add(new LevelGrowthEntry { coreAttrId = "力量", perLevel = 2f });

            var c = p.Clone();
            c.displayName.SetTextValue(0, "改了");
            c.growth[0].perLevel = 99f;
            c.maxLevel = 1;

            Assert.AreEqual("战士", p.displayName.GetTextValue()); // 原对象不受影响
            Assert.AreEqual(2f, p.growth[0].perLevel, 1e-4f);
            Assert.AreEqual(20, p.maxLevel);
        }

        [Test]
        public void TypedFields_DefaultToCorrectType()
        {
            var p = new ProfessionDefinition("warrior");
            Assert.AreEqual(EFieldType.Text,   p.displayName.Type);
            Assert.AreEqual(EFieldType.Text,   p.description.Type);
            Assert.AreEqual(EFieldType.Sprite, p.icon.Type);
        }

        // ── ProfessionTree ────────────────────────────────────────────────────────

        [Test]
        public void Roots_AreNodesWithNoParent()
        {
            var tree = new ProfessionTree("warrior_line");
            tree.nodes.Add(new ProfessionTreeNode { professionRef = "warrior", childProfessionRefs = { "knight", "berserker" } });
            tree.nodes.Add(new ProfessionTreeNode { professionRef = "knight" });
            tree.nodes.Add(new ProfessionTreeNode { professionRef = "berserker" });

            var roots = new List<string>(tree.Roots());
            Assert.AreEqual(1, roots.Count);
            Assert.AreEqual("warrior", roots[0]);
            Assert.IsNotNull(tree.FindNode("knight"));
            Assert.IsNull(tree.FindNode("mage"));
        }
    }
}
