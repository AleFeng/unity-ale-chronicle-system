using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ale.Chronicle;
using Ale.Chronicle.Runtime.UI;

namespace Ale.Chronicle.Tests
{
    /// <summary>
    /// 技能运行时管理器·集成门槛：
    /// <list type="bullet">
    ///   <item><b>提供层</b>（装备等外部来源）：全量替换、有效集变化才触发事件、多来源留存、不误删永久、合并保序、存档隔离、Reset 清两层；</item>
    ///   <item><b>施放派发</b>：<see cref="SkillRuntimeManager.UseSkill"/> 校验存在→触发 <see cref="SkillRuntimeManager.OnSkillUsed"/>，无状态副作用；</item>
    ///   <item><b>采集器</b>：角色来源现取「有效」技能（永久 ∪ 提供）。</item>
    /// </list>
    /// </summary>
    public class SkillRuntimeManagerTests
    {
        private readonly List<ChronicleDatabase> _created = new List<ChronicleDatabase>();

        private ChronicleDatabase NewDb()
        {
            var db = ScriptableObject.CreateInstance<ChronicleDatabase>();
            _created.Add(db);
            return db;
        }

        private ChronicleDatabase NewDbWithSkills(params string[] ids)
        {
            var db = NewDb();
            foreach (var id in ids) db.Skills.Add(new Skill(id));
            ChronicleDataManager.Instance.Register(db);
            return db;
        }

        [TearDown]
        public void Cleanup()
        {
            ChronicleDataManager.Instance.ClearDatabases();   // 防止跨测试串数据
            SkillRuntimeManager.Instance.ResetAll();          // 清空永久 + 提供层
            foreach (var db in _created)
                if (db != null) UnityEngine.Object.DestroyImmediate(db);
            _created.Clear();
        }

        // ── 提供层 ───────────────────────────────────────────────────────────

        [Test]
        public void SetProvidedSkills_FiresOnLearnedChanged_OnlyWhenEffectiveChanges()
        {
            var mgr = SkillRuntimeManager.Instance;
            int events = 0;
            Action<string> h = id => { if (id == "c") events++; };
            mgr.OnLearnedChanged += h;

            Assert.IsTrue(mgr.SetProvidedSkills("c", "equip", new[] { "a" }), "新来源 → 有效集变化");
            Assert.AreEqual(1, events);

            Assert.IsFalse(mgr.SetProvidedSkills("c", "equip", new[] { "a" }), "同一集合 → 来源无变化，不触发");
            Assert.AreEqual(1, events);

            Assert.IsTrue(mgr.SetProvidedSkills("c", "equip", new[] { "a", "b" }));
            Assert.AreEqual(2, events);

            mgr.Learn("c", "x");                                            // 永久层变化 → 触发一次
            Assert.AreEqual(3, events);
            Assert.IsFalse(mgr.SetProvidedSkills("c", "equip2", new[] { "x" }),
                "提供 x，但 x 已永久 → 有效集不变，不触发");
            Assert.AreEqual(3, events);

            mgr.OnLearnedChanged -= h;
        }

        [Test]
        public void MultiProvider_SkillStaysUntilLastProviderCleared()
        {
            var mgr = SkillRuntimeManager.Instance;
            mgr.SetProvidedSkills("c", "A", new[] { "s" });
            mgr.SetProvidedSkills("c", "B", new[] { "s" });
            Assert.IsTrue(mgr.HasSkill("c", "s"));

            Assert.IsFalse(mgr.ClearProvider("c", "A"), "B 仍提供 s → 有效集不变");
            Assert.IsTrue(mgr.HasSkill("c", "s"), "仍有来源提供 → 保留");

            Assert.IsTrue(mgr.ClearProvider("c", "B"), "最后来源移除 → 有效集变化");
            Assert.IsFalse(mgr.HasSkill("c", "s"));
        }

        [Test]
        public void ClearProvider_NeverRemovesPermanentSkill()
        {
            var mgr = SkillRuntimeManager.Instance;
            mgr.Learn("c", "s");
            mgr.SetProvidedSkills("c", "equip", new[] { "s" });   // s 已永久 → 有效集不变（但来源仍记账）

            Assert.IsFalse(mgr.ClearProvider("c", "equip"), "s 仍永久 → 有效集不变");
            Assert.IsTrue(mgr.HasLearned("c", "s"), "永久层不被卸下来源影响");
            Assert.IsTrue(mgr.HasSkill("c", "s"));
        }

        [Test]
        public void GetEffectiveSkillIds_UnionDedup_PermanentFirst()
        {
            var mgr = SkillRuntimeManager.Instance;
            mgr.Learn("c", "p1");
            mgr.Learn("c", "p2");
            mgr.SetProvidedSkills("c", "equip", new[] { "p2", "e1" });   // p2 与永久重叠

            CollectionAssert.AreEqual(new[] { "p1", "p2", "e1" },
                new List<string>(mgr.GetEffectiveSkillIds("c")), "永久在前、去重、保序");
        }

        [Test]
        public void GetEffectiveSkills_ResolvesViaDataManager_SkipsUnknownId()
        {
            var mgr = SkillRuntimeManager.Instance;
            NewDbWithSkills("a", "b");
            mgr.Learn("c", "a");
            mgr.SetProvidedSkills("c", "equip", new[] { "b", "ghost" });   // ghost 解析不到

            var skills = mgr.GetEffectiveSkills("c");
            CollectionAssert.AreEqual(new[] { "a", "b" }, skills.ConvertAll(s => s.id));
        }

        [Test]
        public void SaveData_ExcludesProvidedLayer_And_LoadFiresNoEvent()
        {
            var mgr = SkillRuntimeManager.Instance;
            mgr.Learn("c", "perm");
            mgr.SetProvidedSkills("c", "equip", new[] { "prov" });

            var save  = mgr.GetSaveData();
            var entry = save.Find(s => s.characterId == "c");
            Assert.IsNotNull(entry);
            CollectionAssert.AreEqual(new[] { "perm" }, entry.skillIds, "存档只含永久层，不含提供层");

            int events = 0;
            Action<string> h = _ => events++;
            mgr.OnLearnedChanged += h;
            var data = new List<RuntimeLearnedSkillState> { new RuntimeLearnedSkillState("c") };
            data[0].skillIds.Add("z");
            mgr.LoadSaveData(data);
            Assert.AreEqual(0, events, "加载不触发事件");
            Assert.IsTrue(mgr.HasLearned("c", "z"), "只恢复永久层");
            mgr.OnLearnedChanged -= h;
        }

        [Test]
        public void ResetAll_ClearsBothLayers()
        {
            var mgr = SkillRuntimeManager.Instance;
            mgr.Learn("c", "p");
            mgr.SetProvidedSkills("c", "equip", new[] { "e" });

            mgr.ResetAll();
            Assert.IsFalse(mgr.HasLearned("c", "p"));
            Assert.IsFalse(mgr.HasSkill("c", "e"));
            Assert.AreEqual(0, mgr.GetEffectiveSkillIds("c").Count);
        }

        // ── 施放派发 ─────────────────────────────────────────────────────────

        [Test]
        public void UseSkill_ExistingSkill_FiresOnSkillUsed_ReturnsTrue()
        {
            var mgr = SkillRuntimeManager.Instance;
            NewDbWithSkills("heal");

            SkillUseEvent? captured = null;
            Action<SkillUseEvent> h = e => captured = e;
            mgr.OnSkillUsed += h;

            Assert.IsTrue(mgr.UseSkill("hero", "heal", "potion1"));
            Assert.IsTrue(captured.HasValue);
            Assert.AreEqual("hero",    captured.Value.TargetCharacterId);
            Assert.AreEqual("heal",    captured.Value.SkillId);
            Assert.AreEqual("potion1", captured.Value.SourceKey);

            mgr.OnSkillUsed -= h;
        }

        [Test]
        public void UseSkill_UnknownSkill_ReturnsFalse_NoEvent()
        {
            var mgr = SkillRuntimeManager.Instance;
            NewDbWithSkills("heal");

            int uses = 0;
            Action<SkillUseEvent> h = _ => uses++;
            mgr.OnSkillUsed += h;

            Assert.IsFalse(mgr.UseSkill("hero", "ghost"));
            Assert.AreEqual(0, uses);

            mgr.OnSkillUsed -= h;
        }

        [Test]
        public void UseSkill_HasNoStateSideEffects()
        {
            var mgr = SkillRuntimeManager.Instance;
            NewDbWithSkills("heal");

            mgr.UseSkill("hero", "heal");
            Assert.IsFalse(mgr.HasLearned("hero", "heal"), "施放不改永久层");
            Assert.IsFalse(mgr.HasSkill("hero", "heal"),   "施放不改提供层");
            Assert.AreEqual(0, mgr.GetSaveData().Count,    "施放不入存档");
        }

        // ── 采集器整合 ───────────────────────────────────────────────────────

        [Test]
        public void Collect_Character_IncludesProvidedSkills()
        {
            var mgr = SkillRuntimeManager.Instance;
            NewDbWithSkills("a", "b");
            mgr.Learn("hero", "a");
            mgr.SetProvidedSkills("hero", "equip", new[] { "b" });

            var list = SkillCollector.Collect(ESkillSource.Character, "hero");
            CollectionAssert.AreEqual(new[] { "a", "b" }, list.ConvertAll(s => s.id),
                "角色来源 = 永久 ∪ 提供");
        }
    }
}
