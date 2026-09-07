using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ale.Chronicle;
using Ale.Modifier;

namespace Ale.Chronicle.Tests
{
    /// <summary>
    /// 运行时特质管理器门槛：永久 / 临时授予、配置层合并（Has / 有效特质去重、配置已持有不重复）、互斥拒绝（互斥列表 + 等价组）、
    /// 到期移除与事件、重复授予的刷新 / 保持与叠层、时长约定（0 按定义 / >0 指定 / <0 永久）、按来源成组撤销、
    /// 运行时修饰器收集与 <see cref="ChronicleCharacterRuntime"/> 汇流、存档往返。
    /// </summary>
    public class TraitRuntimeManagerTests
    {
        private ChronicleDatabase _db;
        private TraitRuntimeManager _mgr;

        [SetUp]
        public void Setup()
        {
            ResetManagers();
            _db = ScriptableObject.CreateInstance<ChronicleDatabase>();

            _db.CoreAttributes.Add(new CoreAttributeDefinition("might") { minValue = 0f, maxValue = 100f, defaultBase = 10f });

            var brave = new TraitDefinition("brave");
            brave.modifiers.Add(new ModifierDefinition("might", EModifierOperation.Add, 3f));
            _db.Traits.Add(brave);

            var coward = new TraitDefinition("coward");
            coward.incompatibleTraitRefs.Add("brave");
            _db.Traits.Add(coward);

            var sanguine = new TraitDefinition("sanguine") { groupEquivalenceRef = "temperament" };
            sanguine.modifiers.Add(new ModifierDefinition("might", EModifierOperation.Add, 5f));
            _db.Traits.Add(sanguine);
            _db.Traits.Add(new TraitDefinition("melancholic") { groupEquivalenceRef = "temperament" });

            _db.Traits.Add(new TraitDefinition("fever") { lifetime = ETraitLifetime.Temporary, defaultDurationDays = 3f, durationStacksRefresh = true });
            _db.Traits.Add(new TraitDefinition("chill") { lifetime = ETraitLifetime.Temporary, defaultDurationDays = 2f, durationStacksRefresh = false });
            _db.Traits.Add(new TraitDefinition("void")  { lifetime = ETraitLifetime.Temporary, defaultDurationDays = 0f });

            var c = new CharacterDefinition("c");
            c.traits.Add(new CharacterTraitInstance("brave"));
            _db.Characters.Add(c);
            _db.Characters.Add(new CharacterDefinition("d"));

            ChronicleDataManager.Instance.Register(_db);
            _mgr = TraitRuntimeManager.Instance;
        }

        [TearDown]
        public void Cleanup()
        {
            ResetManagers();
            if (_db) Object.DestroyImmediate(_db);
            _db = null;
        }

        private static void ResetManagers()
        {
            ChronicleDataManager.Instance.ClearDatabases();
            TraitRuntimeManager.Instance.ResetAll();
            EffectRuntimeManager.Instance.ResetAll();
            ChronicleClock.Instance.ResetAll();
        }

        // ── 授予 / 合并 / 互斥 ────────────────────────────────────────────────────────

        [Test]
        public void Grant_Permanent_MergesWithConfigLayer()
        {
            Assert.IsTrue(_mgr.Has("c", "brave"), "配置层特质计入 Has");
            Assert.IsFalse(_mgr.HasRuntime("c", "brave"));
            Assert.IsFalse(_mgr.Grant("c", "brave"), "配置层已持有 → 不重复授予");

            int granted = 0;
            _mgr.OnTraitGranted += (id, t) => { if (id == "c" && t == "sanguine") granted++; };
            Assert.IsTrue(_mgr.Grant("c", "sanguine"));
            Assert.AreEqual(1, granted);
            Assert.IsTrue(_mgr.HasRuntime("c", "sanguine"));
            Assert.IsTrue(_mgr.TryGetRuntime("c", "sanguine", out var inst));
            Assert.IsTrue(inst.IsPermanent);

            var effective = _mgr.GetEffectiveTraits("c");
            CollectionAssert.AreEqual(new[] { "brave", "sanguine" }, effective.ConvertAll(t => t.traitRef));

            Assert.IsFalse(_mgr.Grant("c", "nope"), "未定义的特质");
            Assert.IsFalse(_mgr.Grant("c", "void"), "临时特质无有效时长 → 不授予");
        }

        [Test]
        public void Grant_RejectsIncompatible_ListAndEquivalenceGroup()
        {
            Assert.IsFalse(_mgr.Grant("c", "coward"), "与配置层 brave 互斥");
            Assert.IsTrue(_mgr.Grant("c", "sanguine"));
            Assert.IsFalse(_mgr.Grant("c", "melancholic"), "同等价组互斥");
            Assert.IsTrue(_mgr.Grant("d", "coward"), "另一角色无 brave → 可授予");
        }

        [Test]
        public void Temporary_ExpiresOnTick_FiresEvents()
        {
            var revoked = new List<string>();
            int changed = 0;
            _mgr.OnTraitRevoked  += (id, t) => revoked.Add(id + ":" + t);
            _mgr.OnTraitsChanged += id => { if (id == "c") changed++; };

            Assert.IsTrue(_mgr.Grant("c", "fever"));
            Assert.IsTrue(_mgr.TryGetRuntime("c", "fever", out var inst));
            Assert.AreEqual(3f, inst.remainingDays);
            Assert.AreEqual(1, changed);

            _mgr.Tick(2f);
            Assert.IsTrue(_mgr.TryGetRuntime("c", "fever", out inst));
            Assert.AreEqual(1f, inst.remainingDays);
            Assert.AreEqual(0, revoked.Count);

            _mgr.Tick(1f);
            Assert.IsFalse(_mgr.Has("c", "fever"));
            CollectionAssert.AreEqual(new[] { "c:fever" }, revoked);
            Assert.AreEqual(3, changed, "授予 + 两次 Tick（临时特质变化）");
        }

        [Test]
        public void Regrant_RefreshesOrKeeps_AndStacks()
        {
            _mgr.Grant("c", "fever");
            _mgr.Tick(2f);
            Assert.IsTrue(_mgr.Grant("c", "fever"), "合并");
            _mgr.TryGetRuntime("c", "fever", out var fever);
            Assert.AreEqual(3f, fever.remainingDays, "durationStacksRefresh → 刷新到默认时长");
            Assert.AreEqual(2, fever.stacks);

            _mgr.Grant("c", "chill");
            _mgr.Tick(1f);
            _mgr.Grant("c", "chill");
            _mgr.TryGetRuntime("c", "chill", out var chill);
            Assert.AreEqual(1f, chill.remainingDays, "不刷新 → 保持剩余");
            Assert.AreEqual(2, chill.stacks);
        }

        [Test]
        public void DurationConvention_ZeroByDefinition_PositiveOverride_NegativePermanent()
        {
            _mgr.Grant("c", "fever", 10f);
            _mgr.TryGetRuntime("c", "fever", out var fever);
            Assert.AreEqual(10f, fever.remainingDays);

            _mgr.Grant("c", "chill", -1f);
            _mgr.TryGetRuntime("c", "chill", out var chill);
            Assert.IsTrue(chill.IsPermanent, "强制永久");

            _mgr.Grant("d", "sanguine", 5f);
            _mgr.TryGetRuntime("d", "sanguine", out var sang);
            Assert.AreEqual(5f, sang.remainingDays, "永久定义也可指定天数");
            _mgr.Tick(5f);
            Assert.IsFalse(_mgr.Has("d", "sanguine"));
        }

        [Test]
        public void RevokeBySource_RemovesGroup()
        {
            _mgr.Grant("c", "fever", 0f, 1, "effect:x");
            _mgr.Grant("c", "sanguine", 0f, 1, "effect:x");
            _mgr.Grant("c", "chill", 0f, 1, "effect:y");

            Assert.AreEqual(2, _mgr.RevokeBySource("c", "effect:x"));
            Assert.IsFalse(_mgr.Has("c", "fever"));
            Assert.IsFalse(_mgr.Has("c", "sanguine"));
            Assert.IsTrue(_mgr.Has("c", "chill"));
            Assert.AreEqual(0, _mgr.RevokeBySource("c", "effect:x"));

            Assert.IsTrue(_mgr.Revoke("c", "chill"));
            Assert.IsFalse(_mgr.Revoke("c", "chill"));
            Assert.AreEqual(0, _mgr.GetRuntimeTraits("c").Count);
        }

        // ── 汇流 ───────────────────────────────────────────────────────────────────

        [Test]
        public void Modifiers_RuntimeOnlyCollected_And_CharacterRuntimeMergesConfig()
        {
            var runtimeOnly = new List<ModifierDefinition>();
            _mgr.CollectModifiers("c", "might", runtimeOnly);
            Assert.AreEqual(0, runtimeOnly.Count);

            Assert.AreEqual(13f, ChronicleCharacterRuntime.EvaluateValue("c", "might"), "基础 10 + 配置 brave +3");

            _mgr.Grant("c", "sanguine");
            _mgr.CollectModifiers("c", "might", runtimeOnly);
            Assert.AreEqual(1, runtimeOnly.Count);
            Assert.AreEqual("trait:sanguine", runtimeOnly[0].sourceTag);

            var eval = ChronicleCharacterRuntime.Evaluate("c", "might");
            Assert.AreEqual(18f, eval.Value, "基础 10 + brave 3 + sanguine 5");
            Assert.AreEqual(2, eval.Breakdown.Count);

            Assert.AreEqual(10f, ChronicleCharacterRuntime.EvaluateValue("unknown", "might"), "未知角色按默认基础值");
        }

        // ── 存档 ───────────────────────────────────────────────────────────────────

        [Test]
        public void SaveRoundTrip()
        {
            _mgr.Grant("c", "fever", 0f, 2, "effect:x");
            _mgr.Grant("d", "coward");
            _mgr.Tick(1f);

            var data = _mgr.GetSaveData();
            _mgr.ResetAll();
            Assert.IsFalse(_mgr.HasRuntime("c", "fever"));

            _mgr.LoadSaveData(data);
            Assert.IsTrue(_mgr.TryGetRuntime("c", "fever", out var fever));
            Assert.AreEqual(2f, fever.remainingDays);
            Assert.AreEqual(2, fever.stacks);
            Assert.AreEqual("effect:x", fever.sourceTag);
            Assert.IsTrue(_mgr.HasRuntime("d", "coward"));

            _mgr.Tick(2f);
            Assert.IsFalse(_mgr.Has("c", "fever"));
        }
    }
}
