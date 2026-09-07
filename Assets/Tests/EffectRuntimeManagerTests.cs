using System;
using NUnit.Framework;
using UnityEngine;
using Ale.Chronicle;
using Ale.Condition;
using Ale.Effect;
using Ale.GameplayTags;
using Ale.Modifier;

namespace Ale.Chronicle.Tests
{
    /// <summary>
    /// 效果运行时（C2）门槛：持续效果施加 → 属性汇流 +10（明细含 effect:{id}#h）→ 叠加刷新 → 时钟推进到期回落；
    /// 瞬时效果经 Chronicle.GrantTrait 授予临时特质（智力 −20）→ 3 年后消失；免疫阻断；周期效果逐日永久落地且不随到期回退；
    /// 施加条件走运行时条件源（配置 ∪ 运行时特质）；头衔 / 技能 / 职业经验执行器；存档往返（效果 + 特质 + 时钟）；
    /// UseSkill 施加 onUseEffectRefs 并在事件中报告；时钟事件；按标签驱散。效果定义来自 toolkit 效果库（<see cref="EffectDataManager"/>，0.5.0）。
    /// </summary>
    public class EffectRuntimeManagerTests
    {
        private ChronicleDatabase    _db;
        private EffectDatabase       _edb;
        private EffectRuntimeManager _em;
        private TraitRuntimeManager  _tm;
        private ChronicleClock       _clock;

        [SetUp]
        public void Setup()
        {
            ResetManagers();
            _db  = ScriptableObject.CreateInstance<ChronicleDatabase>();
            _edb = ScriptableObject.CreateInstance<EffectDatabase>();

            _db.CoreAttributes.Add(new CoreAttributeDefinition("might")     { minValue = 0f, maxValue = 100f,  defaultBase = 10f });
            _db.CoreAttributes.Add(new CoreAttributeDefinition("intellect") { minValue = 0f, maxValue = 100f,  defaultBase = 30f });
            _db.CoreAttributes.Add(new CoreAttributeDefinition("stamina")   { minValue = 0f, maxValue = 1000f, defaultBase = 0f });

            _db.Traits.Add(new TraitDefinition("brave"));
            var deranged = new TraitDefinition("deranged") { lifetime = ETraitLifetime.Temporary, defaultDurationDays = 1095f };
            deranged.modifiers.Add(new ModifierDefinition("intellect", EModifierOperation.Add, -20f));
            _db.Traits.Add(deranged);

            var hero = new CharacterDefinition("hero");
            hero.coreAttributes.Add(new CoreAttributeValue { attrId = "might", baseValue = 20f });
            hero.traits.Add(new CharacterTraitInstance("brave"));
            _db.Characters.Add(hero);
            var weakling = new CharacterDefinition("weakling");
            weakling.coreAttributes.Add(new CoreAttributeValue { attrId = "might", baseValue = 5f });
            _db.Characters.Add(weakling);

            // 战意：持续 30 天，战力 +10，ByTarget 叠加上限 1（重复施加刷新时长）
            var focus = new EffectEntry("battle_focus", EDurationPolicy.HasDuration);
            focus.definition.duration     = EffectMagnitude.Scalable(30f);
            focus.definition.stackingType = EEffectStackingType.AggregateByTarget;
            focus.definition.stackLimit   = 1;
            focus.definition.modifiers.Add(new EffectModifier("might", EModifierOperation.Add, 10f));
            focus.definition.assetTags.AddTag("Status.Buff.Might");
            _edb.Effects.Add(focus);

            // 精神篡改：瞬时，onApply 授予「精神异常」（按定义 1095 天）
            var tamper = new EffectEntry("mind_tamper");
            tamper.definition.assetTags.AddTag("Status.Mental.Tamper");
            var tg    = new EffectGroup(EffectPhases.OnApply);
            var grant = new EffectItem("Chronicle.GrantTrait");
            grant.parameters.Add(PStr("traitId", "deranged"));
            tg.items.Add(grant);
            tamper.definition.executions.groups.Add(tg);
            _edb.Effects.Add(tamper);

            // 心智护盾：无限，免疫 Status.Mental.*
            var ward = new EffectEntry("mental_ward", EDurationPolicy.Infinite);
            ward.definition.grantedApplicationImmunityTags.AddTag("Status.Mental");
            _edb.Effects.Add(ward);

            // 回复：持续 5 天、周期 1 天、耐力 +5 永久落地（施加时不结算）
            var regen = new EffectEntry("regen", EDurationPolicy.HasDuration);
            regen.definition.duration = EffectMagnitude.Scalable(5f);
            regen.definition.period   = EffectMagnitude.Scalable(1f);
            regen.definition.executePeriodicOnApplication = false;
            regen.definition.modifiers.Add(new EffectModifier("stamina", EModifierOperation.Add, 5f));
            _edb.Effects.Add(regen);

            // 门控增益：施加条件 = 主体拥有特质 brave
            var gated = new EffectEntry("gated_buff", EDurationPolicy.HasDuration);
            gated.definition.duration = EffectMagnitude.Scalable(10f);
            gated.definition.modifiers.Add(new EffectModifier("stamina", EModifierOperation.Add, 1f));
            var cg = new ConditionGroup { itemOperator = ConditionLogicOp.And };
            var ci = new ConditionItem("Chronicle.HasTrait");
            ci.parameters.Add(CInt("scope", (int)EConditionScope.Actor));
            ci.parameters.Add(CStr("traitId", "brave"));
            cg.items.Add(ci);
            gated.definition.applicationCondition.groups.Add(cg);
            _edb.Effects.Add(gated);

            // 奖赏：瞬时，授予头衔 + 学会技能 + 职业经验
            var reward = new EffectEntry("reward");
            var rg = new EffectGroup(EffectPhases.OnApply);
            var t1 = new EffectItem("Chronicle.GrantTitle");       t1.parameters.Add(PStr("titleId", "veteran"));       rg.items.Add(t1);
            var s1 = new EffectItem("Chronicle.LearnSkill");       s1.parameters.Add(PStr("skillId", "spark"));         rg.items.Add(s1);
            var p1 = new EffectItem("Chronicle.AddProfessionExp"); p1.parameters.Add(PStr("professionId", "warrior")); p1.parameters.Add(PInt("amount", 10)); rg.items.Add(p1);
            reward.definition.executions.groups.Add(rg);
            _edb.Effects.Add(reward);

            _db.Titles.Add(new TitleDefinition("veteran") { kind = ETitleKind.Epithet });
            var warrior = new ProfessionDefinition("warrior") { maxLevel = 3 };
            warrior.expCurve.mode = EExpCurveMode.Table;
            warrior.expCurve.perLevelExp.AddRange(new[] { 10, 20 });
            _db.Professions.Add(warrior);

            var skill = new Skill("focus");
            skill.onUseEffectRefs.Add("battle_focus");
            skill.onUseEffectRefs.Add("mind_tamper");
            _db.Skills.Add(skill);
            _db.Skills.Add(new Skill("spark"));

            _edb.NormalizeAll();
            Assert.IsTrue(_db.Validate(out var errors), string.Join("\n", errors));
            Assert.IsTrue(_edb.Validate(out var effectErrors), string.Join("\n", effectErrors));

            ChronicleDataManager.Instance.Register(_db);
            EffectDataManager.Instance.Register(_edb);
            _em    = EffectRuntimeManager.Instance;
            _tm    = TraitRuntimeManager.Instance;
            _clock = ChronicleClock.Instance;
        }

        [TearDown]
        public void Cleanup()
        {
            ResetManagers();
            if (_db)  UnityEngine.Object.DestroyImmediate(_db);
            if (_edb) UnityEngine.Object.DestroyImmediate(_edb);
            _db  = null;
            _edb = null;
        }

        private static void ResetManagers()
        {
            ChronicleDataManager.Instance.ClearDatabases();
            EffectDataManager.Instance.ClearDatabases();
            EffectRuntimeManager.Instance.ResetAll();
            TraitRuntimeManager.Instance.ResetAll();
            ChronicleClock.Instance.ResetAll();
            TitleRuntimeManager.Instance.ResetAll();
            ProfessionRuntimeManager.Instance.ResetAll();
            SkillRuntimeManager.Instance.ResetAll();
            EffectContainer.DefaultRandom = () => 0f;
        }

        private static EffectParam PStr(string id, string v)     { var p = new EffectParam(id, EffectParamType.String);       p.SetString(v); return p; }
        private static EffectParam PInt(string id, long v)       { var p = new EffectParam(id, EffectParamType.Int);          p.SetInt(v);    return p; }
        private static ConditionParam CInt(string id, long v)    { var p = new ConditionParam(id, ConditionParamType.Int);    p.SetInt(v);    return p; }
        private static ConditionParam CStr(string id, string v)  { var p = new ConditionParam(id, ConditionParamType.String); p.SetString(v); return p; }

        private static float Might(string id)     => ChronicleCharacterRuntime.EvaluateValue(id, "might");
        private static float Intellect(string id) => ChronicleCharacterRuntime.EvaluateValue(id, "intellect");
        private static float Stamina(string id)   => ChronicleCharacterRuntime.EvaluateValue(id, "stamina");

        // ── 持续效果：汇流 / 叠加 / 到期 ────────────────────────────────────────────

        [Test]
        public void Apply_DurationEffect_AggregatesThenExpires()
        {
            Assert.AreEqual(20f, Might("hero"));

            var r = _em.Apply("battle_focus", "hero");
            Assert.AreEqual(EEffectApplyOutcome.Applied, r.Outcome, r.ToString());
            Assert.AreEqual(1, r.Handle);

            var eval = ChronicleCharacterRuntime.Evaluate("hero", "might");
            Assert.AreEqual(30f, eval.Value);
            bool traced = false;
            foreach (var c in eval.Breakdown) if (c.SourceTag == "effect:battle_focus#1") traced = true;
            Assert.IsTrue(traced, "明细可追溯到效果实例");
            Assert.IsTrue(_em.HasEffect("hero", "battle_focus"));

            var r2 = _em.Apply("battle_focus", "hero");
            Assert.AreEqual(EEffectApplyOutcome.Refreshed, r2.Outcome, "ByTarget 上限 1 → 刷新时长不加层");
            Assert.AreEqual(1, _em.GetActiveEffects("hero").Count);

            _clock.AdvanceDays(29);
            Assert.AreEqual(30f, Might("hero"));
            _clock.AdvanceDays(1);
            Assert.AreEqual(20f, Might("hero"), "30 天到期回落");
            Assert.AreEqual(0, _em.GetActiveEffects("hero").Count);
        }

        // ── 瞬时效果 → 执行器授予临时特质 ───────────────────────────────────────────

        [Test]
        public void Apply_InstantEffect_GrantsTemporaryTrait_ThatExpires()
        {
            Assert.AreEqual(30f, Intellect("hero"));

            var r = _em.Apply("mind_tamper", "hero");
            Assert.IsTrue(r.IsSuccess, r.ToString());
            Assert.AreEqual(0, r.Handle, "瞬时效果不入容器");
            Assert.IsTrue(_tm.TryGetRuntime("hero", "deranged", out var inst));
            Assert.AreEqual(1095f, inst.remainingDays, "0 → 按特质定义的默认天数");
            Assert.AreEqual("effect:mind_tamper", inst.sourceTag);
            Assert.AreEqual(10f, Intellect("hero"), "精神异常：智力 −20");

            _clock.AdvanceYears(3);
            Assert.IsFalse(_tm.Has("hero", "deranged"));
            Assert.AreEqual(30f, Intellect("hero"));
        }

        // ── 免疫 ────────────────────────────────────────────────────────────────────

        [Test]
        public void Immunity_BlocksTaggedEffect_UntilRemoved()
        {
            Assert.IsTrue(_em.Apply("mental_ward", "hero").IsSuccess);
            var r = _em.Apply("mind_tamper", "hero");
            Assert.AreEqual(EEffectApplyOutcome.BlockedByImmunity, r.Outcome, r.ToString());
            Assert.IsFalse(_tm.Has("hero", "deranged"));
            Assert.IsTrue(_em.Apply("mind_tamper", "weakling").IsSuccess, "其他角色不受影响");

            Assert.AreEqual(1, _em.RemoveById("hero", "mental_ward"));
            Assert.IsTrue(_em.Apply("mind_tamper", "hero").IsSuccess);
        }

        // ── 周期效果永久落地 ────────────────────────────────────────────────────────

        [Test]
        public void Periodic_LandsPermanently_AndOutlivesEffect()
        {
            int periodic = 0;
            Action<string, ActiveEffect> h = (id, e) => periodic++;
            _em.OnPeriodicExecuted += h;
            try
            {
                Assert.IsTrue(_em.Apply("regen", "hero").IsSuccess);
                Assert.AreEqual(0f, Stamina("hero"), "周期效果的修饰器不入汇流，且施加时不结算");

                for (int i = 0; i < 5; i++) _clock.AdvanceDays(1);
                Assert.AreEqual(5, periodic);
                Assert.AreEqual(25f, Stamina("hero"));
                Assert.AreEqual(0, _em.GetActiveEffects("hero").Count, "5 天到期");
                var perm = _em.GetPermanentModifiers("hero");
                Assert.AreEqual(5, perm.Count);
                Assert.AreEqual("effect:regen#1", perm[0].sourceTag);

                _clock.AdvanceDays(10);
                Assert.AreEqual(25f, Stamina("hero"), "永久落地不随效果移除回退");
            }
            finally { _em.OnPeriodicExecuted -= h; }
        }

        // ── 施加条件走运行时条件源 ──────────────────────────────────────────────────

        [Test]
        public void ApplicationCondition_UsesRuntimeConditionSource()
        {
            Assert.IsTrue(_em.Apply("gated_buff", "hero").IsSuccess, "hero 配置层持有 brave");
            var r = _em.Apply("gated_buff", "weakling");
            Assert.AreEqual(EEffectApplyOutcome.BlockedByCondition, r.Outcome, r.ToString());

            _tm.Grant("weakling", "brave");
            Assert.IsTrue(_em.Apply("gated_buff", "weakling").IsSuccess, "运行时授予的特质同样满足条件");
        }

        // ── 头衔 / 技能 / 职业经验执行器 ────────────────────────────────────────────

        [Test]
        public void Executors_TitleSkillProfession()
        {
            ProfessionRuntimeManager.Instance.Learn("hero", "warrior");
            var r = _em.Apply("reward", "hero");
            Assert.IsTrue(r.IsSuccess, r.ToString());
            Assert.IsTrue(TitleRuntimeManager.Instance.Has("hero", "veteran"));
            Assert.IsTrue(ChronicleCharacterRuntime.HasTitle("hero", "veteran"));
            Assert.IsTrue(SkillRuntimeManager.Instance.HasLearned("hero", "spark"));
            Assert.AreEqual(2, ProfessionRuntimeManager.Instance.GetLevel("hero", "warrior"), "10 经验升到 2 级");
            Assert.AreEqual(2, ChronicleCharacterRuntime.GetProfessionLevel("hero", "warrior"));
        }

        // ── 存档往返 ────────────────────────────────────────────────────────────────

        [Test]
        public void SaveRoundTrip_RestoresEffects_Permanent_Traits_Clock()
        {
            _em.Apply("battle_focus", "hero");
            _em.Apply("regen", "hero");
            _em.Apply("mind_tamper", "hero");
            _clock.AdvanceDays(2);
            Assert.AreEqual(30f, Might("hero"));
            Assert.AreEqual(10f, Stamina("hero"));

            var effects = _em.GetSaveData();
            var traits  = _tm.GetSaveData();
            var clock   = _clock.GetSaveData();

            _em.ResetAll(); _tm.ResetAll(); _clock.ResetAll();
            Assert.AreEqual(20f, Might("hero"));
            Assert.AreEqual(0, _clock.WorldDay);

            _em.LoadSaveData(effects); _tm.LoadSaveData(traits); _clock.LoadSaveData(clock);
            Assert.AreEqual(2, _clock.WorldDay);
            Assert.AreEqual(30f, Might("hero"));
            Assert.AreEqual(10f, Stamina("hero"));
            Assert.AreEqual(10f, Intellect("hero"));
            Assert.AreEqual(2, _em.GetActiveEffects("hero").Count);
            var focus = _em.GetContainer("hero").Find("battle_focus");
            Assert.IsNotNull(focus);
            Assert.AreEqual(28f, focus.Remaining);

            _clock.AdvanceDays(3);
            Assert.AreEqual(25f, Stamina("hero"), "恢复后周期继续结算");
            Assert.IsFalse(_em.HasEffect("hero", "regen"));
            _clock.AdvanceDays(25);
            Assert.IsFalse(_em.HasEffect("hero", "battle_focus"));
            Assert.AreEqual(20f, Might("hero"));
        }

        // ── UseSkill ────────────────────────────────────────────────────────────────

        [Test]
        public void UseSkill_AppliesOnUseEffects_AndReportsInEvent()
        {
            var sm = SkillRuntimeManager.Instance;
            SkillUseEvent last = default;
            int fired = 0;
            Action<SkillUseEvent> h = e => { last = e; fired++; };
            sm.OnSkillUsed += h;
            try
            {
                Assert.IsTrue(sm.UseSkill("hero", "focus", "test", "weakling"));
                Assert.AreEqual(1, fired);
                Assert.AreEqual("hero", last.TargetCharacterId);
                Assert.AreEqual("weakling", last.SourceCharacterId);
                Assert.AreEqual(2, last.EffectResults.Count);
                Assert.AreEqual(2, last.EffectsApplied);
                Assert.AreEqual(30f, Might("hero"));
                Assert.IsTrue(_tm.Has("hero", "deranged"));
                Assert.AreEqual("weakling", _em.GetContainer("hero").Find("battle_focus").Source, "来源角色进入活动实例");

                Assert.IsTrue(sm.UseSkill("hero", "spark"));
                Assert.AreEqual(0, last.EffectsApplied);
                Assert.AreEqual(0, last.EffectResults.Count);
                Assert.IsFalse(sm.UseSkill("hero", "nope"));
            }
            finally { sm.OnSkillUsed -= h; }
        }

        // ── 时钟 / 驱散 ─────────────────────────────────────────────────────────────

        [Test]
        public void Clock_AdvanceDays_FiresEvent_IgnoresNonPositive()
        {
            int days = -1, day = -1;
            Action<int, int> h = (d, w) => { days = d; day = w; };
            _clock.OnDaysAdvanced += h;
            try
            {
                _clock.AdvanceDays(0);
                Assert.AreEqual(-1, days);
                _clock.AdvanceDays(5);
                Assert.AreEqual(5, days);
                Assert.AreEqual(5, day);
                Assert.AreEqual(5, _clock.WorldDay);
                _clock.SetWorldDay(100);
                Assert.AreEqual(100, _clock.WorldDay);
                Assert.AreEqual(5, days, "SetWorldDay 不触发推进事件");
            }
            finally { _clock.OnDaysAdvanced -= h; }
        }

        [Test]
        public void RemoveWithTag_Dispels()
        {
            _em.Apply("battle_focus", "hero");
            Assert.AreEqual(1, _em.RemoveWithTag("hero", "Status.Buff"));
            Assert.IsFalse(_em.HasEffect("hero", "battle_focus"));
            Assert.AreEqual(20f, Might("hero"));
        }
    }
}
