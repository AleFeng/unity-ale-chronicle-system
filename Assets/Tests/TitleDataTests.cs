using System.Collections.Generic;
using NUnit.Framework;
using Ale.Chronicle;
using Ale.Toolkit.Runtime;

namespace Ale.Chronicle.Tests
{
    /// <summary>
    /// 头衔系统 Step 2 数据层门槛：TitleDefinition 的修饰器汇流（只收 modifiers、排除 opinionModifiers、
    /// 打 title:{id} 来源标记、目标过滤、保留显式来源）、显示名回退、深拷贝、默认 kind / 字段类型；RankLadder 深拷贝。
    /// </summary>
    public class TitleDataTests
    {
        [Test]
        public void Kind_DefaultsToRankTitle()
        {
            Assert.AreEqual(ETitleKind.RankTitle, new TitleDefinition("duke").kind);
        }

        [Test]
        public void TypedFields_DefaultToCorrectType()
        {
            var t = new TitleDefinition("duke");
            Assert.AreEqual(EFieldType.Text,   t.displayName.Type);
            Assert.AreEqual(EFieldType.Text,   t.description.Type);
            Assert.AreEqual(EFieldType.Sprite, t.icon.Type);
        }

        [Test]
        public void CollectModifiers_OnlyModifiers_NotOpinion_WithSourceTag()
        {
            var t = new TitleDefinition("duke");
            t.modifiers.Add(new ModifierDefinition("统帅", EModifierOperation.Add, 5f, null));
            t.opinionModifiers.Add(new ModifierDefinition("统帅", EModifierOperation.Add, 99f, null));

            var into = new List<ModifierDefinition>();
            t.CollectModifiers("统帅", into);

            Assert.AreEqual(1, into.Count, "opinionModifiers 不应被收集");
            Assert.AreEqual("统帅", into[0].targetAttributeId);
            Assert.AreEqual(5f, into[0].magnitude, 1e-4f);
            Assert.AreEqual("title:duke", into[0].sourceTag);
        }

        [Test]
        public void CollectModifiers_TargetFilter_And_PreservesExplicitSourceTag()
        {
            var t = new TitleDefinition("duke");
            t.modifiers.Add(new ModifierDefinition("统帅", EModifierOperation.Add, 5f, "custom"));
            t.modifiers.Add(new ModifierDefinition("魅力", EModifierOperation.Add, 3f, null));

            var into = new List<ModifierDefinition>();
            t.CollectModifiers("统帅", into);

            Assert.AreEqual(1, into.Count);
            Assert.AreEqual("custom", into[0].sourceTag, "已有来源标记应被保留");

            into.Clear();
            t.CollectModifiers(null, into);   // null 目标 = 收集全部
            Assert.AreEqual(2, into.Count);
        }

        [Test]
        public void PlainName_FallsBackToId()
        {
            var t = new TitleDefinition("duke");
            Assert.AreEqual("duke", t.PlainName());
            t.displayName.SetTextValue(0, "公爵");
            Assert.AreEqual("公爵", t.PlainName());
        }

        [Test]
        public void Clone_IsDeep()
        {
            var t = new TitleDefinition("duke") { kind = ETitleKind.RankTitle, rankTier = 5, isUnique = true };
            t.displayName.SetTextValue(0, "公爵");
            t.modifiers.Add(new ModifierDefinition("统帅", EModifierOperation.Add, 5f, null));
            t.opinionModifiers.Add(new ModifierDefinition("威望", EModifierOperation.Add, 10f, null));

            var c = t.Clone();
            c.displayName.SetTextValue(0, "改了");
            c.modifiers[0].magnitude = 99f;
            c.rankTier = 0;

            Assert.AreEqual("公爵", t.displayName.GetTextValue()); // 原对象不受影响
            Assert.AreEqual(5f, t.modifiers[0].magnitude, 1e-4f);
            Assert.AreEqual(1, c.opinionModifiers.Count);
            Assert.AreEqual(5, t.rankTier);
        }

        [Test]
        public void RankLadder_Clone_IsDeep()
        {
            var l = new RankLadder("peerage");
            l.orderedTitleRefs.AddRange(new[] { "baron", "viscount" });

            var c = l.Clone();
            c.orderedTitleRefs.Add("earl");

            Assert.AreEqual(2, l.orderedTitleRefs.Count, "克隆的列表修改不应影响原对象");
            Assert.AreEqual(3, c.orderedTitleRefs.Count);
        }
    }
}
