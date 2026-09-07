using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using Ale.Chronicle;
using Ale.Chronicle.Serialization;
using Ale.Toolkit.Runtime;
using Ale.Toolkit.Runtime.Serialization;
using Ale.Modifier;
using Ale.Effect;
using Ale.GameplayTags;

#pragma warning disable 618   // 本文件专门覆盖 0.4.0 legacy 效果字段的读取与迁移

namespace Ale.Chronicle.Tests
{
    /// <summary>
    /// 效果外移（0.5.0）门槛：编年史库不再校验技能效果引用、数据管理器不再充当效果定义源（效果按 id 经 toolkit EffectDataManager /
    /// 全局注册表解析）；二进制 v8 不写效果 / 标签块而保住技能 onUseEffectRefs；旧 v7 文件的效果 / 标签读入 legacy 字段；
    /// legacy → toolkit EffectDatabase 迁移（显示字段 / 定义深拷贝保留、同 id 冲突跳过并留在 legacy、标签去重、无冲突即清空）。
    /// </summary>
    public class ChronicleEffectDatabaseTests
    {
        private readonly List<Object> _created = new List<Object>();

        private ChronicleDatabase NewDb()
        {
            var db = ScriptableObject.CreateInstance<ChronicleDatabase>();
            _created.Add(db);
            return db;
        }

        private EffectDatabase NewEffectDb()
        {
            var db = ScriptableObject.CreateInstance<EffectDatabase>();
            _created.Add(db);
            return db;
        }

        [TearDown]
        public void Cleanup()
        {
            ChronicleDataManager.Instance.ClearDatabases();
            EffectDataManager.Instance.ClearDatabases();
            foreach (var o in _created)
                if (o) Object.DestroyImmediate(o);
            _created.Clear();
        }

        /// <summary>0.4.0 形态的 legacy 效果：战意（持续 30、战力 +10、ByTarget 叠加、标签、显示名 / 描述）。</summary>
        private static ChronicleEffect LegacyFocus()
        {
            var focus = new ChronicleEffect("battle_focus", EDurationPolicy.HasDuration);
            focus.displayText.SetTextValue(0, "战意");
            focus.descriptionText.SetTextValue(0, "战力 +10，持续 30 天");
            focus.definition.duration     = EffectMagnitude.Scalable(30f);
            focus.definition.stackingType = EEffectStackingType.AggregateByTarget;
            focus.definition.stackLimit   = 1;
            focus.definition.modifiers.Add(new EffectModifier("might", EModifierOperation.Add, 10f));
            focus.definition.assetTags.AddTag("Status.Buff.Might");
            focus.Normalize();
            return focus;
        }

        // ── 校验 / 数据管理器 ───────────────────────────────────────────────────────

        [Test]
        public void Validate_SkillEffectRefs_NoLongerChecked()
        {
            var db    = NewDb();
            var skill = new Skill("focus");
            skill.onUseEffectRefs.Add("not_in_any_library");   // 效果在 toolkit 效果库，编年史库不再作悬空校验
            db.Skills.Add(skill);
            Assert.IsTrue(db.Validate(out var errors), string.Join("\n", errors));
        }

        [Test]
        public void DataManager_NotADefinitionSource_EffectDataManagerResolves()
        {
            int before = EffectDefinitionRegistry.Default.SourceCount;
            ChronicleDataManager.Instance.Register(NewDb());
            Assert.AreEqual(before, EffectDefinitionRegistry.Default.SourceCount, "编年史数据管理器不再登记为效果定义源");
            Assert.IsNull(EffectDefinitionRegistry.Default.GetEffect("battle_focus"));

            var edb = NewEffectDb();
            edb.Effects.Add(ChronicleLegacyEffects.ToEntry(LegacyFocus()));
            EffectDataManager.Instance.Register(edb);
            Assert.AreEqual("battle_focus", EffectDefinitionRegistry.Default.GetEffect("battle_focus")?.id);
            Assert.AreEqual("战意", EffectDataManager.Instance.GetEffect("battle_focus").ResolveDisplayName());
        }

        // ── 序列化 v8 / v7 ─────────────────────────────────────────────────────────

        [Test]
        public void Serializer_V8_RoundTrip_KeepsSkillRefs_WritesNoEffectBlocks()
        {
            var src = NewDb();
            src.CoreAttributes.Add(new CoreAttributeDefinition("might"));
            var skill = new Skill("focus");
            skill.onUseEffectRefs.Add("battle_focus");
            skill.onUseEffectRefs.Add("mind_tamper");
            src.Skills.Add(skill);
            src.LegacyEffects.Add(LegacyFocus());
            src.LegacyGameplayTags.Add(new GameplayTagDefinition("Status.Buff.Might", "战意类增益"));

            Assert.AreEqual(8, ChronicleConfigSerializer.Version);
            var dst = ChronicleConfigSerializer.Import(ChronicleConfigSerializer.Export(src));
            _created.Add(dst);

            CollectionAssert.AreEqual(new[] { "battle_focus", "mind_tamper" }, dst.GetSkill("focus").onUseEffectRefs);
            Assert.AreEqual(0, dst.LegacyEffects.Count, "v8 不再写出效果块");
            Assert.AreEqual(0, dst.LegacyGameplayTags.Count, "v8 不再写出 Gameplay 标签块");
            Assert.IsFalse(dst.HasLegacyEffectData);
        }

        [Test]
        public void Serializer_OldV7File_LoadsEffectsAndTagsIntoLegacy()
        {
            // 手工构造「全空 v7 + 1 效果 + 1 标签」字节流：魔数 + 版本 7 + 19 个空块 + 效果块 + 标签块（块 = 计数 + 条目；条目布局与 0.4.0 写端一致）。
            var legacy   = LegacyFocus();
            var resolver = NullAssetRefResolver.Instance;
            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                using (var w = new BinaryWriter(ms, Encoding.UTF8))
                {
                    w.Write(0x4348524F);
                    w.Write(7);
                    for (int i = 0; i < 19; i++) w.Write(0);
                    w.Write(1);                                                                  // 效果块：1 条
                    ToolkitBinaryCodec.WriteStr(w, legacy.id);
                    ToolkitBinaryCodec.WriteValue(w, ToolkitDtoMapper.ToDto(legacy.displayText, resolver));
                    ToolkitBinaryCodec.WriteValue(w, ToolkitDtoMapper.ToDto(legacy.descriptionText, resolver));
                    ToolkitBinaryCodec.WriteValue(w, ToolkitDtoMapper.ToDto(legacy.iconValue, resolver));
                    ToolkitBinaryCodec.WriteStr(w, EffectJson.ToJson(legacy.definition, false));
                    w.Write(1);                                                                  // 标签块：1 条
                    ToolkitBinaryCodec.WriteStr(w, "Status.Buff.Might");
                    ToolkitBinaryCodec.WriteStr(w, "战意类增益");
                }
                bytes = ms.ToArray();
            }

            var dst = ChronicleConfigSerializer.Import(bytes);
            _created.Add(dst);
            Assert.IsTrue(dst.HasLegacyEffectData);
            Assert.AreEqual(1, dst.LegacyEffects.Count);
            var e = dst.LegacyEffects[0];
            Assert.AreEqual("battle_focus", e.id);
            Assert.AreEqual("战意", e.displayText.GetTextValue(0));
            Assert.AreEqual("战力 +10，持续 30 天", e.descriptionText.GetTextValue(0));
            Assert.AreEqual(EDurationPolicy.HasDuration, e.definition.durationPolicy);
            Assert.AreEqual(30f, e.definition.duration.baseValue);
            Assert.AreEqual(EEffectStackingType.AggregateByTarget, e.definition.stackingType);
            Assert.AreEqual("might", e.definition.modifiers[0].attributeId);
            Assert.IsTrue(e.definition.assetTags.HasTagExact(GameplayTag.Parse("Status.Buff.Might")));
            Assert.AreEqual(1, dst.LegacyGameplayTags.Count);
            Assert.AreEqual("Status.Buff.Might", dst.LegacyGameplayTags[0].name);
            Assert.AreEqual("战意类增益", dst.LegacyGameplayTags[0].comment);
            Assert.AreEqual(0, dst.Skills.Count);
        }

        // ── 迁移 ───────────────────────────────────────────────────────────────────

        [Test]
        public void Migration_MovesEffectsAndTags_SkipsConflicts_ClearsLegacyOnceClean()
        {
            var src = NewDb();
            var legacyFocus = LegacyFocus();
            src.LegacyEffects.Add(legacyFocus);
            var tamper = new ChronicleEffect("mind_tamper");
            var grp = new EffectGroup(EffectPhases.OnApply);
            grp.items.Add(new EffectItem("Chronicle.GrantTrait"));
            tamper.definition.executions.groups.Add(grp);
            src.LegacyEffects.Add(tamper);
            src.LegacyEffects.Add(new ChronicleEffect("dup"));
            src.LegacyEffects.Add(new ChronicleEffect(""));                                     // 空 id → 忽略
            src.LegacyGameplayTags.Add(new GameplayTagDefinition("Status.Buff.Might", "战意类增益"));
            src.LegacyGameplayTags.Add(new GameplayTagDefinition("Immunity.Mental"));

            var dst = NewEffectDb();
            dst.Effects.Add(new EffectEntry("dup"));                                            // 同 id 冲突
            dst.GameplayTags.Add(new GameplayTagDefinition("Immunity.Mental", "已有"));          // 同名标签

            var report = ChronicleLegacyEffects.MigrateInto(src, dst);

            Assert.AreEqual(2, report.MigratedCount);
            CollectionAssert.AreEqual(new[] { "dup" }, report.SkippedEffectIds);
            Assert.AreEqual(1, report.InvalidEffects);
            Assert.AreEqual(1, report.TagsAdded);
            Assert.AreEqual(1, report.TagsSkipped);
            Assert.IsFalse(report.LegacyCleared, "有冲突 → 冲突条目留在 legacy");
            Assert.AreEqual(1, src.LegacyEffects.Count);
            Assert.AreEqual("dup", src.LegacyEffects[0].id);
            Assert.AreEqual(0, src.LegacyGameplayTags.Count, "标签已全部并入或已存在 → legacy 标签清空");

            var focus = dst.GetEffect("battle_focus");
            Assert.IsNotNull(focus);
            Assert.AreEqual("战意", focus.displayText.GetTextValue(0));
            Assert.AreEqual("战力 +10，持续 30 天", focus.descriptionText.GetTextValue(0));
            Assert.AreEqual(EFieldType.Sprite, focus.iconValue.Type);
            Assert.IsTrue(string.IsNullOrEmpty(focus.templateRef), "不挂模板");
            Assert.AreEqual("battle_focus", focus.definition.id);
            Assert.AreEqual("战意", focus.definition.displayName);
            Assert.AreEqual(30f, focus.definition.duration.baseValue);
            Assert.AreEqual(EEffectStackingType.AggregateByTarget, focus.definition.stackingType);
            Assert.AreEqual("might", focus.definition.modifiers[0].attributeId);
            Assert.IsTrue(focus.definition.assetTags.HasTagExact(GameplayTag.Parse("Status.Buff.Might")));
            Assert.AreNotSame(legacyFocus.definition, focus.definition, "定义深拷贝");
            Assert.AreEqual("Chronicle.GrantTrait", dst.GetEffect("mind_tamper").definition.executions.groups[0].items[0].key);
            Assert.AreEqual(2, dst.GameplayTags.Count);
            Assert.AreEqual("已有", dst.GameplayTags[0].comment, "已存在的标签不被覆盖");
            Assert.IsTrue(dst.Validate(out var errors), string.Join("\n", errors));

            // 用户处理冲突（删掉目标库的 dup）后重跑：剩余条目迁入，legacy 清空
            dst.Effects.RemoveAll(e => e.id == "dup");
            var second = ChronicleLegacyEffects.MigrateInto(src, dst);
            Assert.AreEqual(1, second.MigratedCount);
            Assert.IsFalse(second.HasConflicts);
            Assert.IsTrue(second.LegacyCleared);
            Assert.IsFalse(src.HasLegacyEffectData);
            Assert.AreEqual(3, dst.Effects.Count);
        }

        [Test]
        public void Migration_DryRun_LeavesLegacyUntouched()
        {
            var src = NewDb();
            src.LegacyEffects.Add(LegacyFocus());
            src.LegacyGameplayTags.Add(new GameplayTagDefinition("Status.Buff.Might"));
            var dst = NewEffectDb();

            var report = ChronicleLegacyEffects.MigrateInto(src, dst, removeFromLegacy: false);

            Assert.AreEqual(1, report.MigratedCount);
            Assert.AreEqual(1, report.TagsAdded);
            Assert.IsFalse(report.LegacyCleared);
            Assert.AreEqual(1, src.LegacyEffects.Count);
            Assert.AreEqual(1, src.LegacyGameplayTags.Count);
            Assert.IsNotNull(dst.GetEffect("battle_focus"));
        }
    }
}
