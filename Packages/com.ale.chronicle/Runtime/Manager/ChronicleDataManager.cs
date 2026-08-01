using System;
using System.Collections.Generic;
using Ale.Toolkit.Runtime;

namespace Ale.Chronicle
{
    /// <summary>
    /// 编年史运行时数据访问单例。统一管理一个或多个 <see cref="ChronicleDatabase"/>，对外提供
    /// 按 id/name 跨库 O(1) 查询（惰性字典索引）。继承 toolkit 的 <see cref="ToolkitSingleton{T}"/>，
    /// 关闭 Domain Reload 时也能在每次播放开始复位静态状态（不串数据）。
    ///
    /// <para>索引在注册 / 注销 / 清空后置脏，下次查询时重建一次；构建按 <see cref="Databases"/> 顺序
    /// 「先注册先得」，与逐库线性查找语义一致。</para>
    /// </summary>
    public class ChronicleDataManager : ToolkitSingleton<ChronicleDataManager>
    {
        private readonly List<ChronicleDatabase> _databases = new List<ChronicleDatabase>();

        /// <summary>已注册的数据库列表。</summary>
        public IReadOnlyList<ChronicleDatabase> Databases => _databases;

        protected override void Init() { }

        #region 注册 / 加载

        /// <summary>注册一个数据库（去重）。</summary>
        public void Register(ChronicleDatabase database)
        {
            if (!database || _databases.Contains(database)) return;
            _databases.Add(database);
            InvalidateIndex();
        }

        /// <summary>注销一个数据库。</summary>
        public void Unregister(ChronicleDatabase database)
        {
            if (!database) return;
            if (_databases.Remove(database)) InvalidateIndex();
        }

        /// <summary>清空所有已注册数据库。</summary>
        public void ClearDatabases()
        {
            _databases.Clear();
            InvalidateIndex();
        }

        /// <summary>从二进制反序列化为一个新的 <see cref="ChronicleDatabase"/> 并注册。</summary>
        public ChronicleDatabase LoadFromBinary(byte[] bytes)
        {
            var db = Serialization.ChronicleConfigSerializer.Import(bytes, null);
            Register(db);
            return db;
        }

        #endregion

        #region 查询索引

        private bool _indexDirty = true;

        private readonly Dictionary<string, EnumType>               _enumTypes = new Dictionary<string, EnumType>();
        private readonly Dictionary<string, Tag>                    _tags      = new Dictionary<string, Tag>();
        private readonly Dictionary<string, CoreAttributeDefinition> _coreAttrs = new Dictionary<string, CoreAttributeDefinition>();
        private readonly Dictionary<string, TraitDefinition>        _traits    = new Dictionary<string, TraitDefinition>();
        private readonly Dictionary<string, CharacterTemplate>      _templates = new Dictionary<string, CharacterTemplate>();
        private readonly Dictionary<string, CharacterDefinition>    _characters = new Dictionary<string, CharacterDefinition>();
        private readonly Dictionary<string, Skill>                  _skills    = new Dictionary<string, Skill>();

        /// <summary>使查询索引失效，下次查询时重建。运行期直接改动已注册数据库内容后需手动调用。</summary>
        public void InvalidateIndex() => _indexDirty = true;

        private void EnsureIndex()
        {
            if (!_indexDirty) return;
            _indexDirty = false;

            _enumTypes.Clear(); _tags.Clear(); _coreAttrs.Clear();
            _traits.Clear();    _templates.Clear(); _characters.Clear();
            _skills.Clear();

            foreach (var db in _databases)
            {
                if (!db) continue;
                Index(_enumTypes,  db.EnumTypesList,     x => x.name);
                Index(_tags,       db.Tags,              x => x.name);
                Index(_coreAttrs,  db.CoreAttributes,    x => x.id);
                Index(_traits,     db.Traits,            x => x.id);
                Index(_templates,  db.CharacterTemplates, x => x.name);
                Index(_characters, db.Characters,        x => x.id);
                Index(_skills,     db.Skills,            x => x.id);
            }
        }

        // 已存在的键不覆盖 —— 「先注册的数据库优先」。
        private static void Index<TValue>(Dictionary<string, TValue> map, List<TValue> source, Func<TValue, string> keyOf)
        {
            if (source == null) return;
            foreach (var v in source)
            {
                if (v == null) continue;
                string key = keyOf(v);
                if (string.IsNullOrEmpty(key) || map.ContainsKey(key)) continue;
                map[key] = v;
            }
        }

        private TValue Lookup<TValue>(Dictionary<string, TValue> map, string key)
        {
            if (string.IsNullOrEmpty(key)) return default;
            EnsureIndex();
            return map.TryGetValue(key, out var v) ? v : default;
        }

        #endregion

        #region 跨库查询

        /// <summary>按名称跨库查找枚举类型，未找到返回 null。</summary>
        public EnumType GetEnumType(string enumName) => Lookup(_enumTypes, enumName);

        /// <summary>按名称跨库查找功能标签，未找到返回 null。</summary>
        public Tag GetTag(string tagName) => Lookup(_tags, tagName);

        /// <summary>按 id 跨库查找核心属性，未找到返回 null。</summary>
        public CoreAttributeDefinition GetCoreAttribute(string attrId) => Lookup(_coreAttrs, attrId);

        /// <summary>按 id 跨库查找特质，未找到返回 null。</summary>
        public TraitDefinition GetTrait(string traitId) => Lookup(_traits, traitId);

        /// <summary>按名称跨库查找角色模板，未找到返回 null。</summary>
        public CharacterTemplate GetCharacterTemplate(string templateName) => Lookup(_templates, templateName);

        /// <summary>按 id 跨库查找角色，未找到返回 null。</summary>
        public CharacterDefinition GetCharacter(string characterId) => Lookup(_characters, characterId);

        /// <summary>按 id 跨库查找技能，未找到返回 null。</summary>
        public Skill GetSkill(string skillId) => Lookup(_skills, skillId);

        #endregion
    }
}
