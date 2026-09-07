using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using Ale.Chronicle;
using Ale.Chronicle.Serialization;
using Ale.Toolkit.Runtime;
using Ale.Modifier;
using Ale.Effect;
using Ale.GameplayTags;
using Ale.Condition;

namespace Ale.Chronicle.Tests
{
    /// <summary>
    /// 效果配置层（C1）门槛：DB 效果实体 / Gameplay 标签 / 技能效果引用的校验（重复 id、悬空引用、定义错误阻断、警告不阻断、
    /// 非法标签名）；v7 二进制往返保住效果定义（时长 / 周期 / 叠加 / 标签 / 修饰器幅度 / 执行 / 施加条件）、显示字段、
    /// 标签声明与技能 onUseEffectRefs；旧 v6 文件仍可导入；数据管理器跨库查找效果、充当 toolkit 效果定义源并注册标签。
    /// </summary>
    public class ChronicleEffectDatabaseTests
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
            ChronicleDataManager.Instance.ClearDatabases();
            foreach (var db in _created)
                if (db != null) Object.DestroyImmediate(db);
            _created.Clear();
        }

        // ── 构造 ────────────────────────────────────────────────────────────────────

        private static void AddAgeAtLeast(ConditionExpression expr, int min)
        {
            var g  = new ConditionGroup { itemOperator = ConditionLogicOp.And };
            var it = new ConditionItem("Chronicle.Age");
            var p  = new ConditionParam("min", ConditionParamType.Int); p.SetInt(min);
            it.parameters.Add(p);
            g.items.Add(it);
            expr.groups.Add(g);
        }

        /// <summary>核心属性 might / stamina；效果 battle_focus（持续 30、战力 +10、ByTarget 叠加、标签）、regen（持续 5 周期 1、耐力 +5 属性幅度）、
        /// mind_tamper（瞬时、onApply 执行 Chronicle.GrantTrait）；技能 focus 引用 battle_focus；两条标签声明。</summary>
        private ChronicleDatabase BuildValid()
        {
            var db = NewDb();
            db.CoreAttributes.Add(new CoreAttributeDefinition("might"));
            db.CoreAttributes.Add(new CoreAttributeDefinition("stamina"));

            var focus = new ChronicleEffect("battle_focus", EDurationPolicy.HasDuration);
            focus.displayText.SetTextValue(0, "战意");
            focus.descriptionText.SetTextValue(0, "战力 +10，持续 30 天");
            focus.definition.duration     = EffectMagnitude.Scalable(30f);
            focus.definition.stackingType = EEffectStackingType.AggregateByTarget;
            focus.definition.stackLimit   = 1;
            focus.definition.modifiers.Add(new EffectModifier("might", EModifierOperation.Add, 10f));
            focus.definition.assetTags.AddTag("Status.Buff.Might");
            focus.definition.grantedTags.AddTag("Status.Buff");
            focus.definition.cueTags.AddTag("Cue.Buff");
            AddAgeAtLeast(focus.definition.applicationCondition, 3);
            db.Effects.Add(focus);

            var regen = new ChronicleEffect("regen", EDurationPolicy.HasDuration);
            regen.definition.duration = EffectMagnitude.Scalable(5f);
            regen.definition.period   = EffectMagnitude.Scalable(1f);
            regen.definition.modifiers.Add(new EffectModifier("stamina", EModifierOperation.Add,
                EffectMagnitude.AttributeBased("might", 0.5f, 0f, 1f)));
            db.Effects.Add(regen);

            var tamper = new ChronicleEffect("mind_tamper");
            var grp = new EffectGroup(EffectPhases.OnApply);
            grp.items.Add(new EffectItem("Chronicle.GrantTrait"));
            tamper.definition.executions.groups.Add(grp);
            db.Effects.Add(tamper);

            var skill = new Skill("focus");
            skill.onUseEffectRefs.Add("battle_focus");
            skill.onUseEffectRefs.Add("mind_tamper");
            db.Skills.Add(skill);

            db.GameplayTags.Add(new GameplayTagDefinition("Status.Buff.Might", "战意类增益"));
            db.GameplayTags.Add(new GameplayTagDefinition("Immunity.Mental"));
            return db;
        }

        private static bool Any(List<string> errors, string fragment)
        {
            foreach (var e in errors) if (e != null && e.Contains(fragment)) return true;
            return false;
        }

        // ── 归一 / 校验 ─────────────────────────────────────────────────────────────

        [Test]
        public void Normalize_SyncsDefinitionIdentity()
        {
            var e = new ChronicleEffect { id = "x", definition = null };
            e.displayText.SetTextValue(0, "名");
            e.Normalize();
            Assert.IsNotNull(e.definition);
            Assert.AreEqual("x", e.definition.id);
            Assert.AreEqual("名", e.definition.displayName);
            Assert.AreEqual("x", new ChronicleEffect("x").PlainName());   // 无名回退 id

            var c = e.Clone();
            Assert.AreNotSame(e.definition, c.definition);
            Assert.AreEqual("x", c.definition.id);
        }

        [Test]
        public void Validate_ValidDatabase_Passes()
        {
            var db = BuildValid();
            Assert.IsTrue(db.Validate(out var errors), string.Join("\n", errors));
        }

        [Test]
        public void Validate_DuplicateEffectId_Fails()
        {
            var db = BuildValid();
            db.Effects.Add(new ChronicleEffect("battle_focus"));
            Assert.IsFalse(db.Validate(out var errors));
            Assert.IsTrue(Any(errors, "效果 id"), string.Join("\n", errors));
        }

        [Test]
        public void Validate_DanglingSkillEffectRef_Fails()
        {
            var db = BuildValid();
            db.Skills[0].onUseEffectRefs.Add("nope");
            Assert.IsFalse(db.Validate(out var errors));
            Assert.IsTrue(Any(errors, "onUseEffectRefs → 效果 'nope'"), string.Join("\n", errors));
        }

        [Test]
        public void Validate_DanglingModifierAttribute_Fails()
        {
            var db = BuildValid();
            db.Effects[0].definition.modifiers[0].attributeId = "ghost";
            db.Effects[1].definition.modifiers[0].magnitude.attributeId = "ghost2";
            Assert.IsFalse(db.Validate(out var errors));
            Assert.IsTrue(Any(errors, "modifiers.attributeId → 属性 'ghost'"), string.Join("\n", errors));
            Assert.IsTrue(Any(errors, "magnitude.attributeId → 属性 'ghost2'"), string.Join("\n", errors));
        }

        [Test]
        public void Validate_DefinitionError_Fails_ButWarningDoesNot()
        {
            // 警告：瞬时效果配 grantedTags → 不阻断
            var db = BuildValid();
            db.Effects[2].definition.grantedTags.AddTag("Status.X");
            Assert.IsTrue(db.Validate(out var errors), string.Join("\n", errors));

            // 错误：HasDuration 但时长为 0 → 阻断
            db.Effects[0].definition.duration = EffectMagnitude.Scalable(0f);
            Assert.IsFalse(db.Validate(out errors));
            Assert.IsTrue(Any(errors, "battle_focus"), string.Join("\n", errors));
            Assert.IsTrue(Any(errors, "duration"), string.Join("\n", errors));
        }

        [Test]
        public void Validate_InvalidGameplayTagName_Fails()
        {
            var db = BuildValid();
            db.GameplayTags.Add(new GameplayTagDefinition("Bad..Name"));
            Assert.IsFalse(db.Validate(out var errors));
            Assert.IsTrue(Any(errors, "Gameplay 标签"), string.Join("\n", errors));
        }

        // ── 序列化 v7 ───────────────────────────────────────────────────────────────

        [Test]
        public void Serializer_V7_RoundTrip_PreservesEffectsTagsAndSkillRefs()
        {
            var src = BuildValid();
            var dst = ChronicleConfigSerializer.Import(ChronicleConfigSerializer.Export(src));
            _created.Add(dst);

            Assert.AreEqual(3, dst.Effects.Count);
            Assert.AreEqual(2, dst.GameplayTags.Count);

            var focus = dst.GetEffect("battle_focus");
            Assert.IsNotNull(focus);
            Assert.AreEqual("战意", focus.displayText.GetTextValue(0));
            Assert.AreEqual("战力 +10，持续 30 天", focus.descriptionText.GetTextValue(0));
            Assert.AreEqual(EFieldType.Sprite, focus.iconValue.Type);
            var d = focus.definition;
            Assert.AreEqual("battle_focus", d.id);
            Assert.AreEqual("战意", d.displayName);
            Assert.AreEqual(EDurationPolicy.HasDuration, d.durationPolicy);
            Assert.AreEqual(30f, d.duration.baseValue);
            Assert.AreEqual(EEffectStackingType.AggregateByTarget, d.stackingType);
            Assert.AreEqual(1, d.stackLimit);
            Assert.AreEqual(1, d.modifiers.Count);
            Assert.AreEqual("might", d.modifiers[0].attributeId);
            Assert.AreEqual(EModifierOperation.Add, d.modifiers[0].operation);
            Assert.AreEqual(10f, d.modifiers[0].magnitude.baseValue);
            Assert.IsTrue(d.assetTags.HasTagExact(GameplayTag.Parse("Status.Buff.Might")));
            Assert.IsTrue(d.grantedTags.HasTagExact(GameplayTag.Parse("Status.Buff")));
            Assert.IsTrue(d.cueTags.HasTagExact(GameplayTag.Parse("Cue.Buff")));
            Assert.AreEqual(1, d.applicationCondition.groups.Count);
            Assert.AreEqual("Chronicle.Age", d.applicationCondition.groups[0].items[0].key);

            var regen = dst.GetEffect("regen").definition;
            Assert.AreEqual(1f, regen.period.baseValue);
            var mag = regen.modifiers[0].magnitude;
            Assert.AreEqual(EMagnitudeKind.AttributeBased, mag.kind);
            Assert.AreEqual("might", mag.attributeId);
            Assert.AreEqual(0.5f, mag.coefficient);
            Assert.AreEqual(1f, mag.postAdd);

            var tamper = dst.GetEffect("mind_tamper").definition;
            Assert.AreEqual(EDurationPolicy.Instant, tamper.durationPolicy);
            Assert.AreEqual(1, tamper.executions.groups.Count);
            Assert.AreEqual(EffectPhases.OnApply, tamper.executions.groups[0].phase);
            Assert.AreEqual("Chronicle.GrantTrait", tamper.executions.groups[0].items[0].key);

            var skill = dst.GetSkill("focus");
            Assert.IsNotNull(skill);
            CollectionAssert.AreEqual(new[] { "battle_focus", "mind_tamper" }, skill.onUseEffectRefs);

            Assert.AreEqual("Status.Buff.Might", dst.GameplayTags[0].name);
            Assert.AreEqual("战意类增益", dst.GameplayTags[0].comment);
            Assert.AreEqual("Immunity.Mental", dst.GameplayTags[1].name);

            Assert.IsTrue(dst.Validate(out var errors), string.Join("\n", errors));
        }

        [Test]
        public void Serializer_OldV6File_ImportsWithoutEffects()
        {
            // 手工构造「全空 v6」字节流：魔数 + 版本 6 + 19 个空块（6 基础 + 4 v2 + 2 v3 + 4 v4 + 2 v5 + 1 v6）。
            // v7 读端应跳过效果 / 标签块，技能 onUseEffectRefs 为空列表，无异常。
            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                using (var w = new BinaryWriter(ms, Encoding.UTF8))
                {
                    w.Write(0x4348524F);
                    w.Write(6);
                    for (int i = 0; i < 19; i++) w.Write(0);
                }
                bytes = ms.ToArray();
            }

            var dst = ChronicleConfigSerializer.Import(bytes);
            _created.Add(dst);
            Assert.AreEqual(0, dst.Effects.Count);
            Assert.AreEqual(0, dst.GameplayTags.Count);
            Assert.AreEqual(0, dst.Skills.Count);
        }

        // ── 数据管理器 ──────────────────────────────────────────────────────────────

        [Test]
        public void DataManager_LooksUpEffects_ActsAsDefinitionSource_RegistersTags()
        {
            var db  = BuildValid();
            var mgr = ChronicleDataManager.Instance;
            mgr.Register(db);

            Assert.AreSame(db.GetEffect("battle_focus"), mgr.GetEffect("battle_focus"));
            Assert.IsNull(mgr.GetEffect("nope"));

            // toolkit 定义源：管理器本身 + 全局定义注册表（跨库按 id 引用）
            IEffectDefinitionSource src = mgr;
            Assert.AreSame(db.GetEffect("regen").definition, src.GetEffect("regen"));
            Assert.AreSame(db.GetEffect("regen").definition, EffectDefinitionRegistry.Default.GetEffect("regen"));
            Assert.AreEqual("regen", EffectDefinitionRegistry.Default.GetEffect("regen").id);   // 索引前已归一同步 id

            // Gameplay 标签并入注册表（含隐式祖先）
            Assert.IsTrue(GameplayTagRegistry.Default.IsRegistered(GameplayTag.Parse("Status.Buff.Might")));
            Assert.IsTrue(GameplayTagRegistry.Default.IsRegistered(GameplayTag.Parse("Status")));
            Assert.AreEqual("战意类增益", GameplayTagRegistry.Default.GetComment(GameplayTag.Parse("Status.Buff.Might")));

            // 清空后不再充当定义源
            mgr.ClearDatabases();
            Assert.IsNull(EffectDefinitionRegistry.Default.GetEffect("regen"));
            Assert.IsNull(mgr.GetEffect("regen"));
        }
    }
}
