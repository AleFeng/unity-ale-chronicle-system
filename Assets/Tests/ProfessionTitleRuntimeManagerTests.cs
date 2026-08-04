using System;
using NUnit.Framework;
using UnityEngine;

namespace Ale.Chronicle.Tests
{
    /// <summary>
    /// 职业/头衔 Step 8 运行时管理器门槛：
    /// Profession——习得/放弃、AddExp 按曲线升级 + 满级封顶、等级解锁授予头衔/触发特质事件、单一主职业、存档往返；
    /// Title——授予/持有、阶级头衔「一序列一持有」晋升替换、唯一头衔易主、Revoke 受 isRevocable 约束、最高位阶、存档往返。
    /// </summary>
    public class ProfessionTitleRuntimeManagerTests
    {
        private ChronicleDatabase _db;
        private ProfessionRuntimeManager _prof;
        private TitleRuntimeManager _title;

        [SetUp]
        public void Setup()
        {
            _db = ScriptableObject.CreateInstance<ChronicleDatabase>();

            var warrior = new ProfessionDefinition("warrior") { maxLevel = 3 };
            warrior.expCurve.mode = EExpCurveMode.Table;
            warrior.expCurve.perLevelExp.AddRange(new[] { 10, 20 });   // Lv1→2 需 10；Lv2→3 需 20
            warrior.unlocks.Add(new LevelUnlock { level = 2, grantTitleRefs = { "veteran" }, grantTraitRefs = { "battle-hardened" } });
            _db.Professions.Add(warrior);

            _db.Titles.Add(new TitleDefinition("veteran") { kind = ETitleKind.Epithet });
            _db.Titles.Add(new TitleDefinition("baron")   { kind = ETitleKind.RankTitle, rankTier = 1 });
            _db.Titles.Add(new TitleDefinition("duke")    { kind = ETitleKind.RankTitle, rankTier = 5 });
            _db.Titles.Add(new TitleDefinition("king")    { kind = ETitleKind.RankTitle, rankTier = 10, isUnique = true });
            _db.Titles.Add(new TitleDefinition("eternal") { kind = ETitleKind.Epithet, isRevocable = false });

            var ladder = new RankLadder("peerage");
            ladder.orderedTitleRefs.AddRange(new[] { "baron", "duke" });
            _db.RankLadders.Add(ladder);

            ChronicleDataManager.Instance.Register(_db);
            _prof  = ProfessionRuntimeManager.Instance;
            _title = TitleRuntimeManager.Instance;
        }

        [TearDown]
        public void Cleanup()
        {
            ChronicleDataManager.Instance.ClearDatabases();
            ProfessionRuntimeManager.Instance.ResetAll();
            TitleRuntimeManager.Instance.ResetAll();
            if (_db) UnityEngine.Object.DestroyImmediate(_db);
            _db = null;
        }

        // ── 职业 ────────────────────────────────────────────────────────────────

        [Test]
        public void Learn_And_Query()
        {
            Assert.IsTrue(_prof.Learn("c", "warrior"));
            Assert.IsFalse(_prof.Learn("c", "warrior"), "已从事 → 忽略");
            Assert.IsTrue(_prof.HasProfession("c", "warrior"));
            Assert.AreEqual(1, _prof.GetLevel("c", "warrior"));
        }

        [Test]
        public void AddExp_LevelsUpPerCurve_And_CapsAtMax()
        {
            _prof.Learn("c", "warrior");
            int levelUps = 0;
            Action<string, string, int> h = (_, _, _) => levelUps++;
            _prof.OnLevelUp += h;

            _prof.AddExp("c", "warrior", 10);                    // 达 Lv1→2 阈值
            Assert.AreEqual(2, _prof.GetLevel("c", "warrior"));
            Assert.AreEqual(0, _prof.GetExp("c", "warrior"));

            _prof.AddExp("c", "warrior", 25);                    // 需 20 → Lv3（满级），溢出丢弃
            Assert.AreEqual(3, _prof.GetLevel("c", "warrior"));
            Assert.AreEqual(0, _prof.GetExp("c", "warrior"), "满级丢弃溢出经验");

            _prof.AddExp("c", "warrior", 100);                   // 已满级 → 不再升级
            Assert.AreEqual(3, _prof.GetLevel("c", "warrior"));
            Assert.AreEqual(2, levelUps);

            _prof.OnLevelUp -= h;
        }

        [Test]
        public void LevelUnlock_GrantsTitle_And_FiresTraitEvent()
        {
            _prof.Learn("c", "warrior");
            string unlockedTrait = null;
            Action<string, string> h = (_, t) => unlockedTrait = t;
            _prof.OnUnlockTrait += h;

            _prof.AddExp("c", "warrior", 10);                    // → Lv2 解锁

            Assert.IsTrue(_title.Has("c", "veteran"), "等级解锁经 TitleRuntimeManager 授予头衔");
            Assert.AreEqual("battle-hardened", unlockedTrait, "等级解锁触发特质事件");

            _prof.OnUnlockTrait -= h;
        }

        [Test]
        public void SetPrimary_IsSingle()
        {
            _prof.Learn("c", "warrior", primary: true);
            _prof.Learn("c", "mage");
            _prof.SetPrimary("c", "mage");

            Assert.IsFalse(_prof.IsPrimary("c", "warrior"));
            Assert.IsTrue(_prof.IsPrimary("c", "mage"));
        }

        [Test]
        public void Profession_SaveLoad_RoundTrips_NoEventOnLoad()
        {
            _prof.Learn("c", "warrior");
            _prof.AddExp("c", "warrior", 15);                    // Lv2, exp 5
            Assert.AreEqual(2, _prof.GetLevel("c", "warrior"));
            Assert.AreEqual(5, _prof.GetExp("c", "warrior"));

            var save = _prof.GetSaveData();
            _prof.ResetAll();
            Assert.IsFalse(_prof.HasProfession("c", "warrior"));

            int events = 0;
            Action<string> h = _ => events++;
            _prof.OnProfessionsChanged += h;
            _prof.LoadSaveData(save);
            Assert.AreEqual(0, events, "加载不触发事件");
            Assert.AreEqual(2, _prof.GetLevel("c", "warrior"));
            Assert.AreEqual(5, _prof.GetExp("c", "warrior"));
            _prof.OnProfessionsChanged -= h;
        }

        // ── 头衔 ────────────────────────────────────────────────────────────────

        [Test]
        public void RankLadder_Grant_ReplacesPreviousRank_OnePerLadder()
        {
            _title.Grant("c", "baron");
            Assert.IsTrue(_title.Has("c", "baron"));

            _title.Grant("c", "duke");                           // 晋升替换
            Assert.IsTrue(_title.Has("c", "duke"));
            Assert.IsFalse(_title.Has("c", "baron"), "同阶梯只持其一");
            Assert.AreEqual(5, _title.GetHighestRankTier("c", "peerage"));
        }

        [Test]
        public void GetHighestRankTier_MinValue_WhenNoneHeld()
        {
            Assert.AreEqual(int.MinValue, _title.GetHighestRankTier("c", "peerage"));
        }

        [Test]
        public void UniqueTitle_TransfersFromPreviousHolder()
        {
            string tId = null, from = null, to = null;
            Action<string, string, string> h = (a, b, c) => { tId = a; from = b; to = c; };
            _title.OnTitleTransferred += h;

            _title.Grant("c1", "king");
            _title.Grant("c2", "king");

            Assert.IsFalse(_title.Has("c1", "king"), "唯一头衔从原持有者剥夺");
            Assert.IsTrue(_title.Has("c2", "king"));
            Assert.AreEqual("king", tId);
            Assert.AreEqual("c1", from);
            Assert.AreEqual("c2", to);

            _title.OnTitleTransferred -= h;
        }

        [Test]
        public void Revoke_RespectsIsRevocable()
        {
            _title.Grant("c", "duke");
            Assert.IsTrue(_title.Revoke("c", "duke"));
            Assert.IsFalse(_title.Has("c", "duke"));

            _title.Grant("c", "eternal");
            Assert.IsFalse(_title.Revoke("c", "eternal"), "isRevocable=false → 拒绝剥夺");
            Assert.IsTrue(_title.Has("c", "eternal"));
        }

        [Test]
        public void Title_SaveLoad_RoundTrips()
        {
            _title.Grant("c", "veteran");
            _title.Grant("c", "duke");

            var save = _title.GetSaveData();
            _title.ResetAll();
            Assert.IsFalse(_title.Has("c", "veteran"));

            _title.LoadSaveData(save);
            Assert.IsTrue(_title.Has("c", "veteran"));
            Assert.IsTrue(_title.Has("c", "duke"));
        }
    }
}
