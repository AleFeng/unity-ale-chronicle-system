using System;
using System.Collections.Generic;
using Ale.Effect.Editor;
using UnityEditor;

namespace Ale.Chronicle.Editor
{
    /// <summary>
    /// 向 toolkit 效果定义绘制器登记编年史的「属性 id」候选（<see cref="IEffectAttributeCatalogProvider"/>，系统名「Chronicle」）：
    /// 扫描工程内全部 <see cref="ChronicleDatabase"/> 资产的核心属性（显示名 (id)），供 Effect Editor 里修饰器 / 属性幅度的属性 id 下拉；
    /// 多个上层系统并存时按系统名分组。资产增删改后重新扫描（<see cref="ChronicleDatabasePostprocessor"/>），属性本身每次按资产实时枚举。
    /// </summary>
    [InitializeOnLoad]
    internal sealed class ChronicleEffectAttributeProvider : IEffectAttributeCatalogProvider
    {
        private static readonly ChronicleEffectAttributeProvider Instance = new ChronicleEffectAttributeProvider();
        private static List<ChronicleDatabase> _databases;

        static ChronicleEffectAttributeProvider()
        {
            EffectDefinitionDrawerHooks.RegisterAttributeProvider(Instance);
        }

        public string SystemName => "Chronicle";

        public IEnumerable<(string id, string display)> GetAttributes()
        {
            _databases ??= Scan();
            var seen = new HashSet<string>();
            foreach (var db in _databases)
            {
                if (!db) continue;
                foreach (var a in db.CoreAttributes)
                {
                    if (a == null || string.IsNullOrEmpty(a.id) || !seen.Add(a.id)) continue;
                    string n = a.displayName != null ? a.displayName.GetTextValue() : null;
                    yield return (a.id, string.IsNullOrEmpty(n) ? a.id : n);
                }
            }
        }

        /// <summary>使数据库资产列表缓存失效（下次访问重新扫描）。</summary>
        internal static void Invalidate() => _databases = null;

        private static List<ChronicleDatabase> Scan()
        {
            var list = new List<ChronicleDatabase>();
            foreach (var guid in AssetDatabase.FindAssets("t:ChronicleDatabase"))
            {
                var db = AssetDatabase.LoadAssetAtPath<ChronicleDatabase>(AssetDatabase.GUIDToAssetPath(guid));
                if (db) list.Add(db);
            }
            return list;
        }
    }

    /// <summary>资产（.asset）增删改后失效编年史数据库资产缓存。</summary>
    internal sealed class ChronicleDatabasePostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] movedTo, string[] movedFrom)
        {
            if (!Touches(imported) && !Touches(deleted) && !Touches(movedTo)) return;
            ChronicleEffectAttributeProvider.Invalidate();
            ChronicleConditionParamProvider.ChronicleConditionParamCatalogs.Invalidate();
        }

        private static bool Touches(string[] paths)
        {
            if (paths == null) return false;
            foreach (var p in paths)
                if (p != null && p.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
