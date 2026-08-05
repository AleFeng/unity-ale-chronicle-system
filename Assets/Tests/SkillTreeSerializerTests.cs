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
    /// 技能树扩展 S3 序列化门槛：v6 二进制往返保住 技能树（三种 kind / 层级 tierKey / 树状前置 / 各处解锁条件 /
    /// 三种技能点获取方式）、职业 skillTreeRefs、核心属性 conditionalModifiers（修改值 + 条件）；
    /// 且旧 v5 文件仍可导入（技能树为空、无异常）。
    /// </summary>
    public class SkillTreeSerializerTests
    {
        private ChronicleDatabase _src, _dst;

        [TearDown]
        public void Cleanup()
        {
            if (_src != null) Object.DestroyImmediate(_src);
            if (_dst != null) Object.DestroyImmediate(_dst);
            _src = _dst = null;
        }

        // 追加一个简单条件项（Chronicle.Age，min）——仅用于条件往返结构验证，不参与求值。
        private static void AddAgeAtLeast(ConditionExpression expr, int min)
        {
            var g = new ConditionGroup { itemOperator = ConditionLogicOp.And };
            var it = new ConditionItem("Chronicle.Age");
            var p = new ConditionParam("min", ConditionParamType.Int); p.SetInt(min);
            it.parameters.Add(p);
            g.items.Add(it);
            expr.groups.Add(g);
        }

        private ChronicleDatabase BuildSource()
        {
            var db = ScriptableObject.CreateInstance<ChronicleDatabase>();

            // 列表技能树：两个技能，其一带解锁条件；三种 mode 的技能点获取条目
            var listTree = new SkillTree("tree_list") { kind = ESkillTreeKind.List };
            listTree.displayName.SetTextValue(0, "基础列表");
            listTree.skills.Add(new SkillTreeEntry { skillRef = "s1" });
            var e2 = new SkillTreeEntry { skillRef = "s2" };
            AddAgeAtLeast(e2.unlockCondition, 5);
            listTree.skills.Add(e2);
            listTree.pointGrants.Add(new SkillPointGrant { points = 1, mode = ESkillPointGrantMode.OnceOnReached });
            listTree.pointGrants.Add(new SkillPointGrant { points = 2, mode = ESkillPointGrantMode.WhileActive });
            var g3 = new SkillPointGrant { points = 3, mode = ESkillPointGrantMode.PerLevelRepeatable };
            AddAgeAtLeast(g3.condition, 8);
            listTree.pointGrants.Add(g3);
            db.SkillTrees.Add(listTree);

            // 层级技能树：两层（首层带条件），技能以 tierKey 关联层级
            var tieredTree = new SkillTree("tree_tiered") { kind = ESkillTreeKind.Tiered };
            var tier1 = new SkillTreeTier { key = "t1" };
            tier1.displayName.SetTextValue(0, "第一层");
            AddAgeAtLeast(tier1.unlockCondition, 10);
            tieredTree.tiers.Add(tier1);
            tieredTree.tiers.Add(new SkillTreeTier { key = "t2" });
            tieredTree.skills.Add(new SkillTreeEntry { skillRef = "s3", tierKey = "t1" });
            tieredTree.skills.Add(new SkillTreeEntry { skillRef = "s4", tierKey = "t2" });
            db.SkillTrees.Add(tieredTree);

            // 树状技能树：child 以 root 为前置
            var treeTree = new SkillTree("tree_tree") { kind = ESkillTreeKind.Tree };
            treeTree.skills.Add(new SkillTreeEntry { skillRef = "root" });
            var child = new SkillTreeEntry { skillRef = "child" };
            child.prerequisiteSkillRefs.Add("root");
            treeTree.skills.Add(child);
            db.SkillTrees.Add(treeTree);

            // 职业引用两个技能树
            var warrior = new ProfessionDefinition("warrior");
            warrior.skillTreeRefs.Add("tree_list");
            warrior.skillTreeRefs.Add("tree_tree");
            db.Professions.Add(warrior);

            // 核心属性条件修改：+5，条件 Age>=20
            var interest = new CoreAttributeDefinition("interest");
            var cm = new ConditionalModifier
            {
                modifier = new ModifierDefinition("interest", EModifierOperation.Add, 5f, null),
            };
            AddAgeAtLeast(cm.condition, 20);
            interest.conditionalModifiers.Add(cm);
            db.CoreAttributes.Add(interest);

            return db;
        }

        [Test]
        public void BinaryRoundTrip_V6_PreservesSkillTreesRefsAndConditionalModifiers()
        {
            _src = BuildSource();
            byte[] bytes = ChronicleConfigSerializer.Export(_src);
            _dst = ChronicleConfigSerializer.Import(bytes);

            // 列表技能树
            var lt = _dst.GetSkillTree("tree_list");
            Assert.IsNotNull(lt);
            Assert.AreEqual(ESkillTreeKind.List, lt.kind);
            Assert.AreEqual("基础列表", lt.displayName.GetTextValue(0));
            Assert.AreEqual(2, lt.skills.Count);
            Assert.AreEqual("s1", lt.skills[0].skillRef);
            Assert.AreEqual(0, lt.skills[0].unlockCondition.TotalItemCount());
            Assert.AreEqual(1, lt.skills[1].unlockCondition.TotalItemCount());
            Assert.AreEqual(3, lt.pointGrants.Count);
            Assert.AreEqual(ESkillPointGrantMode.OnceOnReached, lt.pointGrants[0].mode);
            Assert.AreEqual(1, lt.pointGrants[0].points);
            Assert.AreEqual(ESkillPointGrantMode.WhileActive, lt.pointGrants[1].mode);
            Assert.AreEqual(ESkillPointGrantMode.PerLevelRepeatable, lt.pointGrants[2].mode);
            Assert.AreEqual(1, lt.pointGrants[2].condition.TotalItemCount());

            // 层级技能树
            var tt = _dst.GetSkillTree("tree_tiered");
            Assert.IsNotNull(tt);
            Assert.AreEqual(ESkillTreeKind.Tiered, tt.kind);
            Assert.AreEqual(2, tt.tiers.Count);
            Assert.AreEqual("t1", tt.tiers[0].key);
            Assert.AreEqual("第一层", tt.tiers[0].displayName.GetTextValue(0));
            Assert.AreEqual(1, tt.tiers[0].unlockCondition.TotalItemCount());
            Assert.AreEqual("t1", tt.FindEntry("s3").tierKey);
            Assert.AreEqual("t2", tt.FindEntry("s4").tierKey);

            // 树状技能树
            var rt = _dst.GetSkillTree("tree_tree");
            Assert.IsNotNull(rt);
            Assert.AreEqual(ESkillTreeKind.Tree, rt.kind);
            var childE = rt.FindEntry("child");
            Assert.IsNotNull(childE);
            Assert.Contains("root", childE.prerequisiteSkillRefs);

            // 职业引用
            var w = _dst.GetProfession("warrior");
            Assert.IsNotNull(w);
            Assert.AreEqual(2, w.skillTreeRefs.Count);
            Assert.Contains("tree_list", w.skillTreeRefs);
            Assert.Contains("tree_tree", w.skillTreeRefs);

            // 属性条件修改
            var a = _dst.GetCoreAttribute("interest");
            Assert.IsNotNull(a);
            Assert.AreEqual(1, a.conditionalModifiers.Count);
            Assert.AreEqual("interest", a.conditionalModifiers[0].modifier.targetAttributeId);
            Assert.AreEqual(5f, a.conditionalModifiers[0].modifier.magnitude, 1e-4f);
            Assert.AreEqual(1, a.conditionalModifiers[0].condition.TotalItemCount());
        }

        [Test]
        public void OldV5File_Loads_WithEmptySkillTrees()
        {
            // 手工构造「全空 v5」字节流：魔数 + 版本5 + 18 个空块
            // （6 基础 + 4 v2 + 2 v3 + 4 v4 + 2 v5）。v6 读端应跳过 v6 技能树块，无异常。
            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                using (var w = new BinaryWriter(ms, Encoding.UTF8))
                {
                    w.Write(0x4348524F);                       // "CHRO"
                    w.Write(5);                                // version 5
                    for (int i = 0; i < 18; i++) w.Write(0);   // 18 个空数组
                }
                bytes = ms.ToArray();
            }

            _dst = ChronicleConfigSerializer.Import(bytes);
            Assert.IsNotNull(_dst);
            Assert.AreEqual(0, _dst.SkillTrees.Count);
            Assert.AreEqual(0, _dst.Professions.Count);
            Assert.AreEqual(0, _dst.CoreAttributes.Count);
        }
    }
}
