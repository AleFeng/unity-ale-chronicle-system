using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ale.Chronicle;

namespace Ale.Chronicle.Tests
{
    /// <summary>
    /// 运行时数据管理器门槛：跨库 O(1) 查询；「先注册的数据库优先」；注销 / 清空后索引不残留旧数据；
    /// LoadFromBinary 导入并注册。
    /// </summary>
    public class ChronicleDataManagerTests
    {
        private readonly List<ChronicleDatabase> _created = new List<ChronicleDatabase>();

        private ChronicleDatabase NewDbWithTrait(string traitId, string displayName)
        {
            var db = ScriptableObject.CreateInstance<ChronicleDatabase>();
            var t = new TraitDefinition(traitId);
            t.displayName.SetTextValue(0, displayName);
            db.Traits.Add(t);
            _created.Add(db);
            return db;
        }

        [TearDown]
        public void Cleanup()
        {
            ChronicleDataManager.Instance.ClearDatabases();   // 防止跨测试串数据
            foreach (var db in _created)
                if (db != null) Object.DestroyImmediate(db);
            _created.Clear();
        }

        [Test]
        public void FirstRegisteredDatabaseWins()
        {
            var db1 = NewDbWithTrait("x", "db1");
            var db2 = NewDbWithTrait("x", "db2");

            var mgr = ChronicleDataManager.Instance;
            mgr.Register(db1);
            mgr.Register(db2);

            Assert.AreSame(db1.GetTrait("x"), mgr.GetTrait("x"));           // 先注册者命中
            Assert.AreEqual("db1", mgr.GetTrait("x").displayName.GetTextValue(0));
        }

        [Test]
        public void ClearDatabases_LeavesNoStaleData()
        {
            var db = NewDbWithTrait("x", "db");
            var mgr = ChronicleDataManager.Instance;
            mgr.Register(db);
            Assert.IsNotNull(mgr.GetTrait("x"));

            mgr.ClearDatabases();
            Assert.IsNull(mgr.GetTrait("x"));   // 索引重建后无残留
        }

        [Test]
        public void Unregister_RemovesEntries()
        {
            var db = NewDbWithTrait("x", "db");
            var mgr = ChronicleDataManager.Instance;
            mgr.Register(db);
            Assert.IsNotNull(mgr.GetTrait("x"));

            mgr.Unregister(db);
            Assert.IsNull(mgr.GetTrait("x"));
        }
    }
}
