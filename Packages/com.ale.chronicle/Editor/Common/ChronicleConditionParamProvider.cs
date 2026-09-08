using System;
using System.Collections.Generic;
using Ale.Condition.Editor;
using Ale.Toolkit.Runtime;
using UnityEditor;

namespace Ale.Chronicle.Editor
{
    /// <summary>
    /// 向 toolkit 条件表达式绘制器登记编年史的候选目录（<see cref="IConditionParamCatalogProvider"/>，系统名「Chronicle」）：
    /// 扫描工程内全部 <see cref="ChronicleDatabase"/> 资产，为 属性 / 特质 / 头衔 / 职业 / 阶级序列 五类 id 供给「显示名 (id)」候选，
    /// 于是 <c>Chronicle.AttributeCompare</c> 的 <c>attrId</c>、<c>HasTrait</c> 的 <c>traitId</c> 等由裸文本框变为下拉。
    ///
    /// <para>与同目录的 <see cref="ChronicleEffectAttributeProvider"/> 同款：资产增删改后重新扫描
    /// （<see cref="ChronicleDatabasePostprocessor"/> 一并失效两者），条目本身每次按资产实时枚举。</para>
    /// </summary>
    internal sealed class ChronicleConditionParamProvider : IConditionParamCatalogProvider
    {
        private readonly Func<ChronicleDatabase, IEnumerable<(string id, AttributeValue displayName)>> _select;

        private ChronicleConditionParamProvider(string catalogRef,
            Func<ChronicleDatabase, IEnumerable<(string id, AttributeValue displayName)>> select)
        {
            CatalogRef = catalogRef;
            _select    = select;
        }

        public string SystemName => "Chronicle";
        public string CatalogRef { get; }

        public IEnumerable<(string id, string display)> GetItems()
        {
            var seen = new HashSet<string>();
            foreach (var db in ChronicleConditionParamCatalogs.Databases)
            {
                if (!db) continue;
                foreach (var (id, displayName) in _select(db))
                {
                    if (string.IsNullOrEmpty(id) || !seen.Add(id)) continue;
                    string n = displayName != null ? displayName.GetTextValue() : null;
                    yield return (id, string.IsNullOrEmpty(n) ? id : n);
                }
            }
        }

        // ── 五个目录的登记与资产缓存 ──────────────────────────────────────────────

        [InitializeOnLoad]
        internal static class ChronicleConditionParamCatalogs
        {
            private static List<ChronicleDatabase> _databases;

            static ChronicleConditionParamCatalogs()
            {
                Register(ChronicleConditionCatalogs.Attribute,  db => Pairs(db.CoreAttributes, a => a.id, a => a.displayName));
                Register(ChronicleConditionCatalogs.Trait,      db => Pairs(db.Traits,         t => t.id, t => t.displayName));
                Register(ChronicleConditionCatalogs.Title,      db => Pairs(db.Titles,         t => t.id, t => t.displayName));
                Register(ChronicleConditionCatalogs.Profession, db => Pairs(db.Professions,    p => p.id, p => p.displayName));
                Register(ChronicleConditionCatalogs.RankLadder, db => Pairs(db.RankLadders,    l => l.id, l => l.displayName));
            }

            /// <summary>工程内全部编年史数据库资产（惰性扫描）。</summary>
            internal static IReadOnlyList<ChronicleDatabase> Databases => _databases ??= Scan();

            /// <summary>使数据库资产列表缓存失效（下次访问重新扫描）。</summary>
            internal static void Invalidate() => _databases = null;

            private static void Register(string catalogRef,
                Func<ChronicleDatabase, IEnumerable<(string, AttributeValue)>> select)
                => ConditionDrawerHooks.RegisterProvider(new ChronicleConditionParamProvider(catalogRef, select));

            private static IEnumerable<(string, AttributeValue)> Pairs<T>(List<T> list,
                Func<T, string> idOf, Func<T, AttributeValue> nameOf) where T : class
            {
                if (list == null) yield break;
                foreach (var item in list)
                    if (item != null) yield return (idOf(item), nameOf(item));
            }

            private static List<ChronicleDatabase> Scan()
            {
                var result = new List<ChronicleDatabase>();
                foreach (var guid in AssetDatabase.FindAssets("t:ChronicleDatabase"))
                {
                    var db = AssetDatabase.LoadAssetAtPath<ChronicleDatabase>(AssetDatabase.GUIDToAssetPath(guid));
                    if (db) result.Add(db);
                }
                return result;
            }
        }
    }
}
