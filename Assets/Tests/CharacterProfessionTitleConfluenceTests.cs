using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ale.Chronicle;
using Ale.Toolkit.Runtime;

namespace Ale.Chronicle.Tests
{
    /// <summary>
    /// 职业/头衔 Step 6 汇流门槛：CharacterDefinition.CollectModifiers 在特质之外，追加汇入
    /// 职业每级成长（prof:{id}:growth，随等级折算）与头衔加成（title:{id}），且排除头衔 opinionModifiers；
    /// 端到端 CoreAttributeResolver.Evaluate 反映三来源之和。
    /// </summary>
    public class CharacterProfessionTitleConfluenceTests
    {
        private ChronicleDatabase _db;
        private CoreAttributeDefinition _strDef;

        [SetUp]
        public void Setup()
        {
            _db = ScriptableObject.CreateInstance<ChronicleDatabase>();

            _strDef = new CoreAttributeDefinition("str") { minValue = 0f, maxValue = 1000f, defaultBase = 10f };
            _db.CoreAttributes.Add(_strDef);

            var brave = new TraitDefinition("brave");
            brave.modifiers.Add(new ModifierDefinition("str", EModifierOperation.Add, 5f, null)); // → trait:brave
            _db.Traits.Add(brave);

            var warrior = new ProfessionDefinition("warrior");
            warrior.growth.Add(new LevelGrowthEntry { coreAttrId = "str", perLevel = 2f });        // 每级 +2
            _db.Professions.Add(warrior);

            var duke = new TitleDefinition("duke");
            duke.modifiers.Add(new ModifierDefinition("str", EModifierOperation.Add, 3f, null));   // → title:duke
            _db.Titles.Add(duke);
        }

        [TearDown]
        public void Cleanup()
        {
            if (_db != null) Object.DestroyImmediate(_db);
            _db = null;
        }

        private CharacterDefinition BuildCharacter(int professionLevel)
        {
            var c = new CharacterDefinition("c1");
            c.coreAttributes.Add(new CoreAttributeValue("str", 10f));
            c.traits.Add(new CharacterTraitInstance("brave"));
            c.professions.Add(new CharacterProfession("warrior", professionLevel));
            c.titles.Add(new CharacterTitle("duke"));
            return c;
        }

        [Test]
        public void CollectModifiers_GathersTraitProfessionTitle_WithSourceTags()
        {
            var c = BuildCharacter(5);   // 职业 Lv5 → 成长 2×(5-1)=8

            var mods = new List<ModifierDefinition>();
            c.CollectModifiers("str", _db, mods);

            Assert.AreEqual(3, mods.Count);
            Assert.IsTrue(mods.Exists(m => m.sourceTag == "trait:brave"          && Mathf.Approximately(m.magnitude, 5f)));
            Assert.IsTrue(mods.Exists(m => m.sourceTag == "prof:warrior:growth"  && Mathf.Approximately(m.magnitude, 8f)));
            Assert.IsTrue(mods.Exists(m => m.sourceTag == "title:duke"           && Mathf.Approximately(m.magnitude, 3f)));
        }

        [Test]
        public void Resolver_EndToEnd_SumsAllThreeSources()
        {
            var c = BuildCharacter(5);
            var e = CoreAttributeResolver.Evaluate(c, _strDef, _db);

            // base 10 + trait 5 + prof 8 + title 3 = 26（全为 Add）
            Assert.AreEqual(26f, e.Value, 1e-3f);
            Assert.AreEqual(3, e.Breakdown.Count);
        }

        [Test]
        public void ProfessionGrowth_ScalesWithLevel()
        {
            var c1 = BuildCharacter(1);   // 成长 2×0 = 0
            var mods1 = new List<ModifierDefinition>();
            c1.CollectModifiers("str", _db, mods1);
            Assert.AreEqual(0f, mods1.Find(m => m.sourceTag == "prof:warrior:growth").magnitude, 1e-4f);

            var c10 = BuildCharacter(10);  // 成长 2×9 = 18
            var mods10 = new List<ModifierDefinition>();
            c10.CollectModifiers("str", _db, mods10);
            Assert.AreEqual(18f, mods10.Find(m => m.sourceTag == "prof:warrior:growth").magnitude, 1e-4f);
        }

        [Test]
        public void OpinionModifiers_AreNotCollected()
        {
            _db.GetTitle("duke").opinionModifiers.Add(new ModifierDefinition("str", EModifierOperation.Add, 100f, null));

            var c = BuildCharacter(5);
            var mods = new List<ModifierDefinition>();
            c.CollectModifiers("str", _db, mods);

            Assert.AreEqual(3, mods.Count, "opinionModifiers 不应汇入核心属性");
            Assert.IsFalse(mods.Exists(m => Mathf.Approximately(m.magnitude, 100f)));
        }

        [Test]
        public void TargetFilter_OnlyMatchingAttribute()
        {
            var c = BuildCharacter(5);
            var mods = new List<ModifierDefinition>();
            c.CollectModifiers("敏捷", _db, mods);   // 无来源作用于「敏捷」
            Assert.AreEqual(0, mods.Count);
        }
    }
}
