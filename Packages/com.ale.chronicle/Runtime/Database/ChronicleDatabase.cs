using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Ale.Toolkit.Runtime;

namespace Ale.Chronicle
{
    /// <summary>
    /// 编年史中央数据库（ScriptableObject）：分列注册表，存枚举类型 / 功能标签 / 核心属性 / 特质 /
    /// 角色模板 / 角色。编辑器始终且仅在该资产上工作；二进制 / JSON 仅作单向导出格式。
    ///
    /// <para>实现 <see cref="IEnumTypeSource"/>（供 toolkit 枚举下拉绘制器取用）与
    /// <see cref="IChronicleSchemaSource"/>（供 <see cref="CharacterDefinition.RebuildAttributes"/> /
    /// <see cref="CoreAttributeResolver"/> 反查模板 / 特质 / 标签）。</para>
    ///
    /// <para>查找为 O(n)，本资产是<b>编辑期数据源</b>；运行时高频查询走 <c>ChronicleDataManager</c>（后续步骤，O(1) 字典索引）。
    /// 独立修饰器库 / 想法（thought）等列表待其类型落地后再加列。</para>
    /// </summary>
    [CreateAssetMenu(fileName = "ChronicleDatabase", menuName = "ChronicleSystem/Chronicle Database")]
    public class ChronicleDatabase : ScriptableObject, IEnumTypeSource, IChronicleSchemaSource
    {
        [SerializeField] private List<EnumType>               enumTypes          = new List<EnumType>();
        [SerializeField] private List<Tag>                    tags               = new List<Tag>();
        [SerializeField] private List<CoreAttributeDefinition> coreAttributes    = new List<CoreAttributeDefinition>();
        [SerializeField] private List<TraitDefinition>        traits             = new List<TraitDefinition>();
        [SerializeField] private List<CharacterTemplate>      characterTemplates = new List<CharacterTemplate>();
        [SerializeField] private List<CharacterDefinition>    characters         = new List<CharacterDefinition>();
        [SerializeField] private List<CoreAttributeTemplate>  coreAttributeTemplates = new List<CoreAttributeTemplate>();
        [SerializeField] private List<TraitTemplate>          traitTemplates         = new List<TraitTemplate>();
        [SerializeField] private List<ChronicleGroupTag>      groupTags              = new List<ChronicleGroupTag>();
        [SerializeField] private List<NumberFormatConfig>     numberFormatConfigs    = new List<NumberFormatConfig>();
        [SerializeField] private List<SkillTemplate>          skillTemplates         = new List<SkillTemplate>();
        [SerializeField] private List<Skill>                  skills                 = new List<Skill>();
        [SerializeField] private List<ProfessionDefinition>   professions            = new List<ProfessionDefinition>();
        [SerializeField] private List<ProfessionTree>         professionTrees        = new List<ProfessionTree>();
        [SerializeField] private List<TitleDefinition>        titles                 = new List<TitleDefinition>();
        [SerializeField] private List<RankLadder>             rankLadders            = new List<RankLadder>();

        #region 访问器

        /// <summary>枚举类型 列表。</summary>
        public List<EnumType> EnumTypesList => enumTypes;

        // 显式实现 IEnumTypeSource：以只读视图满足接口（供编辑器绘制器取用）。
        IReadOnlyList<EnumType> IEnumTypeSource.EnumTypes => enumTypes;

        /// <summary>功能标签 列表。</summary>
        public List<Tag> Tags => tags;

        /// <summary>核心属性 列表。</summary>
        public List<CoreAttributeDefinition> CoreAttributes => coreAttributes;

        /// <summary>特质 列表。</summary>
        public List<TraitDefinition> Traits => traits;

        /// <summary>角色模板 列表。</summary>
        public List<CharacterTemplate> CharacterTemplates => characterTemplates;

        /// <summary>角色 列表。</summary>
        public List<CharacterDefinition> Characters => characters;

        /// <summary>核心属性模板 列表。</summary>
        public List<CoreAttributeTemplate> CoreAttributeTemplates => coreAttributeTemplates;

        /// <summary>特质模板 列表。</summary>
        public List<TraitTemplate> TraitTemplates => traitTemplates;

        /// <summary>分组标签 列表。</summary>
        public List<ChronicleGroupTag> GroupTags => groupTags;

        /// <summary>数字格式配置 列表。</summary>
        public List<NumberFormatConfig> NumberFormatConfigs => numberFormatConfigs;

        /// <summary>技能模板 列表。</summary>
        public List<SkillTemplate> SkillTemplates => skillTemplates;

        /// <summary>技能 列表。</summary>
        public List<Skill> Skills => skills;

        /// <summary>职业 列表。</summary>
        public List<ProfessionDefinition> Professions => professions;

        /// <summary>转职树 列表。</summary>
        public List<ProfessionTree> ProfessionTrees => professionTrees;

        /// <summary>头衔 列表。</summary>
        public List<TitleDefinition> Titles => titles;

        /// <summary>阶级序列 列表。</summary>
        public List<RankLadder> RankLadders => rankLadders;

        #endregion

        #region 按键查找

        /// <summary>在列表中按键线性查找首个匹配项，未找到返回 null（键选择器为无捕获 lambda，编译器缓存为静态委托）。</summary>
        private static T Find<T>(List<T> list, string key, Func<T, string> keyOf) where T : class
        {
            if (string.IsNullOrEmpty(key) || list == null) return null;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e != null && keyOf(e) == key) return e;
            }
            return null;
        }

        // ── 按名称 ────────────────────────────────────────────────────────────────

        /// <summary>按名称查找枚举类型，未找到返回 null。</summary>
        public EnumType GetEnumType(string enumName) => Find(enumTypes, enumName, e => e.name);

        /// <summary>按名称查找功能标签，未找到返回 null。</summary>
        public Tag GetTag(string tagName) => Find(tags, tagName, t => t.name);

        /// <summary>按名称查找角色模板，未找到返回 null。</summary>
        public CharacterTemplate GetCharacterTemplate(string templateName) => Find(characterTemplates, templateName, t => t.name);

        /// <summary>按名称查找核心属性模板，未找到返回 null。</summary>
        public CoreAttributeTemplate GetCoreAttributeTemplate(string templateName) => Find(coreAttributeTemplates, templateName, t => t.name);

        /// <summary>按名称查找特质模板，未找到返回 null。</summary>
        public TraitTemplate GetTraitTemplate(string templateName) => Find(traitTemplates, templateName, t => t.name);

        /// <summary>按名称查找数字格式配置，未找到返回 null。</summary>
        public NumberFormatConfig GetNumberFormatConfig(string configName) => Find(numberFormatConfigs, configName, c => c.name);

        /// <summary>按名称查找技能模板，未找到返回 null。</summary>
        public SkillTemplate GetSkillTemplate(string templateName) => Find(skillTemplates, templateName, t => t.name);

        // ── 按 ID ─────────────────────────────────────────────────────────────────

        /// <summary>按 id 查找核心属性，未找到返回 null。</summary>
        public CoreAttributeDefinition GetCoreAttribute(string attrId) => Find(coreAttributes, attrId, a => a.id);

        /// <summary>按 id 查找特质，未找到返回 null。</summary>
        public TraitDefinition GetTrait(string traitId) => Find(traits, traitId, t => t.id);

        /// <summary>按 id 查找角色，未找到返回 null。</summary>
        public CharacterDefinition GetCharacter(string characterId) => Find(characters, characterId, c => c.id);

        /// <summary>按 id 查找分组标签，未找到返回 null。</summary>
        public ChronicleGroupTag GetGroupTag(string tagId) => Find(groupTags, tagId, t => t.id);

        /// <summary>按 id 查找技能，未找到返回 null。</summary>
        public Skill GetSkill(string skillId) => Find(skills, skillId, s => s.id);

        /// <summary>按 id 查找职业，未找到返回 null。</summary>
        public ProfessionDefinition GetProfession(string professionId) => Find(professions, professionId, p => p.id);

        /// <summary>按 id 查找转职树，未找到返回 null。</summary>
        public ProfessionTree GetProfessionTree(string treeId) => Find(professionTrees, treeId, t => t.id);

        /// <summary>按 id 查找头衔，未找到返回 null。</summary>
        public TitleDefinition GetTitle(string titleId) => Find(titles, titleId, t => t.id);

        /// <summary>按 id 查找阶级序列，未找到返回 null。</summary>
        public RankLadder GetRankLadder(string ladderId) => Find(rankLadders, ladderId, l => l.id);

        #endregion

        #region 数据填充

        /// <summary>添加枚举类型（同名已存在则忽略），可选初始枚举项。</summary>
        public void AddEnumType(string enumName, params string[] itemNames)
        {
            if (string.IsNullOrEmpty(enumName) || GetEnumType(enumName) != null) return;
            var et = new EnumType(enumName);
            et.SeedItems(itemNames);
            enumTypes.Add(et);
        }

        /// <summary>添加功能标签（同名已存在则忽略）。</summary>
        public void AddTag(string tagName, string description = null)
        {
            if (string.IsNullOrEmpty(tagName) || GetTag(tagName) != null) return;
            tags.Add(new Tag(tagName, description));
        }

        #endregion

        #region 校验

        /// <summary>
        /// 校验数据有效性：重复 id / name，以及悬空引用（特质 modifier 目标属性 / 功能标签 / 互斥 / 相性特质；
        /// 角色模板 / 特质 / 核心属性 / 家族指针）。返回 true = 无错误。导出前调用以阻止导出非法数据。
        /// </summary>
        public bool Validate(out List<string> errors)
        {
            errors = new List<string>();

            CheckDuplicates(coreAttributes,     a => a.id,   "核心属性 id", errors);
            CheckDuplicates(traits,             t => t.id,   "特质 id",     errors);
            CheckDuplicates(characters,         c => c.id,   "角色 id",     errors);
            CheckDuplicates(characterTemplates,     t => t.name, "角色模板 name", errors);
            CheckDuplicates(coreAttributeTemplates, t => t.name, "属性模板 name", errors);
            CheckDuplicates(traitTemplates,         t => t.name, "特质模板 name", errors);
            CheckDuplicates(enumTypes,          e => e.name, "枚举类型 name", errors);
            CheckDuplicates(tags,               t => t.name, "功能标签 name", errors);
            CheckDuplicates(groupTags,          t => t.id,   "分组标签 id",   errors);
            CheckDuplicates(numberFormatConfigs, c => c.name, "数字格式 name", errors);
            CheckDuplicates(skills,             s => s.id,   "技能 id",       errors);
            CheckDuplicates(skillTemplates,     t => t.name, "技能模板 name", errors);
            CheckDuplicates(professions,        p => p.id,   "职业 id",       errors);
            CheckDuplicates(professionTrees,    t => t.id,   "转职树 id",     errors);
            CheckDuplicates(titles,             t => t.id,   "头衔 id",       errors);
            CheckDuplicates(rankLadders,        l => l.id,   "阶级序列 id",   errors);

            var dangling = new HashSet<string>();

            // 核心属性：类别 → 枚举类型；模板 → 属性模板
            foreach (var a in coreAttributes)
            {
                if (a == null) continue;
                if (!string.IsNullOrEmpty(a.categoryEnumRef) && GetEnumType(a.categoryEnumRef) == null)
                    dangling.Add($"核心属性[{a.id}].categoryEnumRef → 枚举 '{a.categoryEnumRef}'");
                if (!string.IsNullOrEmpty(a.templateRef) && GetCoreAttributeTemplate(a.templateRef) == null)
                    dangling.Add($"核心属性[{a.id}].templateRef → 属性模板 '{a.templateRef}'");
            }

            // 特质：模板 / modifier 目标属性 / 功能标签 / 互斥 / 相性
            foreach (var t in traits)
            {
                if (t == null) continue;
                if (!string.IsNullOrEmpty(t.templateRef) && GetTraitTemplate(t.templateRef) == null)
                    dangling.Add($"特质[{t.id}].templateRef → 特质模板 '{t.templateRef}'");
                if (t.modifiers != null)
                    foreach (var m in t.modifiers)
                        if (m != null && !string.IsNullOrEmpty(m.targetAttributeId) && GetCoreAttribute(m.targetAttributeId) == null)
                            dangling.Add($"特质[{t.id}].modifier.targetAttributeId → 属性 '{m.targetAttributeId}'");
                if (!string.IsNullOrEmpty(t.functionTagRef) && GetTag(t.functionTagRef) == null)
                    dangling.Add($"特质[{t.id}].functionTagRef → 标签 '{t.functionTagRef}'");
                if (t.incompatibleTraitRefs != null)
                    foreach (var r in t.incompatibleTraitRefs)
                        if (!string.IsNullOrEmpty(r) && GetTrait(r) == null)
                            dangling.Add($"特质[{t.id}].incompatibleTraitRefs → 特质 '{r}'");
                if (t.compatibilities != null)
                    foreach (var c in t.compatibilities)
                        if (!string.IsNullOrEmpty(c.otherTraitRef) && GetTrait(c.otherTraitRef) == null)
                            dangling.Add($"特质[{t.id}].compatibilities.otherTraitRef → 特质 '{c.otherTraitRef}'");
            }

            // 角色：模板 / 特质 / 核心属性 / 家族指针
            foreach (var c in characters)
            {
                if (c == null) continue;
                if (!string.IsNullOrEmpty(c.templateRef) && GetCharacterTemplate(c.templateRef) == null)
                    dangling.Add($"角色[{c.id}].templateRef → 模板 '{c.templateRef}'");
                if (c.traits != null)
                    foreach (var ti in c.traits)
                        if (!string.IsNullOrEmpty(ti.traitRef) && GetTrait(ti.traitRef) == null)
                            dangling.Add($"角色[{c.id}].traits → 特质 '{ti.traitRef}'");
                if (c.coreAttributes != null)
                    foreach (var cv in c.coreAttributes)
                        if (!string.IsNullOrEmpty(cv.attrId) && GetCoreAttribute(cv.attrId) == null)
                            dangling.Add($"角色[{c.id}].coreAttributes.attrId → 属性 '{cv.attrId}'");
                AddIfDanglingCharacter(c.id, "fatherRef", c.fatherRef, dangling);
                AddIfDanglingCharacter(c.id, "motherRef", c.motherRef, dangling);
                if (c.childRefs != null)
                    foreach (var kid in c.childRefs)
                        AddIfDanglingCharacter(c.id, "childRefs", kid, dangling);
            }

            // 技能：模板 / 主分组标签 / 副分组标签（分组标签引用统一 groupTags 池）
            foreach (var s in skills)
            {
                if (s == null) continue;
                if (!string.IsNullOrEmpty(s.templateRef) && GetSkillTemplate(s.templateRef) == null)
                    dangling.Add($"技能[{s.id}].templateRef → 技能模板 '{s.templateRef}'");
                if (!string.IsNullOrEmpty(s.primaryGroupTag) && GetGroupTag(s.primaryGroupTag) == null)
                    dangling.Add($"技能[{s.id}].primaryGroupTag → 分组标签 '{s.primaryGroupTag}'");
                if (s.secondaryGroupTags != null)
                    foreach (var g in s.secondaryGroupTags)
                        if (!string.IsNullOrEmpty(g) && GetGroupTag(g) == null)
                            dangling.Add($"技能[{s.id}].secondaryGroupTags → 分组标签 '{g}'");
            }

            // 职业：分组标签 / 成长目标属性 / 解锁授予的特质·头衔
            foreach (var p in professions)
            {
                if (p == null) continue;
                if (!string.IsNullOrEmpty(p.groupTagRef) && GetGroupTag(p.groupTagRef) == null)
                    dangling.Add($"职业[{p.id}].groupTagRef → 分组标签 '{p.groupTagRef}'");
                if (p.growth != null)
                    foreach (var g in p.growth)
                        if (g != null && !string.IsNullOrEmpty(g.coreAttrId) && GetCoreAttribute(g.coreAttrId) == null)
                            dangling.Add($"职业[{p.id}].growth.coreAttrId → 属性 '{g.coreAttrId}'");
                if (p.unlocks != null)
                    foreach (var u in p.unlocks)
                    {
                        if (u == null) continue;
                        if (u.grantTraitRefs != null)
                            foreach (var r in u.grantTraitRefs)
                                if (!string.IsNullOrEmpty(r) && GetTrait(r) == null)
                                    dangling.Add($"职业[{p.id}].unlocks.grantTraitRefs → 特质 '{r}'");
                        if (u.grantTitleRefs != null)
                            foreach (var r in u.grantTitleRefs)
                                if (!string.IsNullOrEmpty(r) && GetTitle(r) == null)
                                    dangling.Add($"职业[{p.id}].unlocks.grantTitleRefs → 头衔 '{r}'");
                    }
            }

            // 头衔：分组标签 / modifier 目标属性（opinionModifiers 面向社交轴，不校验为核心属性）
            foreach (var t in titles)
            {
                if (t == null) continue;
                if (!string.IsNullOrEmpty(t.groupTagRef) && GetGroupTag(t.groupTagRef) == null)
                    dangling.Add($"头衔[{t.id}].groupTagRef → 分组标签 '{t.groupTagRef}'");
                if (t.modifiers != null)
                    foreach (var m in t.modifiers)
                        if (m != null && !string.IsNullOrEmpty(m.targetAttributeId) && GetCoreAttribute(m.targetAttributeId) == null)
                            dangling.Add($"头衔[{t.id}].modifier.targetAttributeId → 属性 '{m.targetAttributeId}'");
            }

            // 转职树：节点 / 子边职业须存在且为同树节点；成环为错误
            foreach (var tree in professionTrees)
            {
                if (tree == null || tree.nodes == null) continue;
                var nodeSet = new HashSet<string>();
                foreach (var n in tree.nodes)
                    if (n != null && !string.IsNullOrEmpty(n.professionRef)) nodeSet.Add(n.professionRef);
                foreach (var n in tree.nodes)
                {
                    if (n == null) continue;
                    if (!string.IsNullOrEmpty(n.professionRef) && GetProfession(n.professionRef) == null)
                        dangling.Add($"转职树[{tree.id}].nodes → 职业 '{n.professionRef}'");
                    if (n.childProfessionRefs != null)
                        foreach (var ch in n.childProfessionRefs)
                        {
                            if (string.IsNullOrEmpty(ch)) continue;
                            if (GetProfession(ch) == null)
                                dangling.Add($"转职树[{tree.id}].childProfessionRefs → 职业 '{ch}'");
                            else if (!nodeSet.Contains(ch))
                                errors.Add($"转职树[{tree.id}] 子职业 '{ch}' 未作为本树节点存在");
                        }
                }
                if (ProfessionTreeHasCycle(tree))
                    errors.Add($"转职树[{tree.id}] 存在环（转职路径不可成环）");
            }

            // 阶级序列：成员头衔须存在、须为阶级头衔(RankTitle)、无重复
            foreach (var l in rankLadders)
            {
                if (l == null || l.orderedTitleRefs == null) continue;
                var seen = new HashSet<string>();
                foreach (var tr in l.orderedTitleRefs)
                {
                    if (string.IsNullOrEmpty(tr)) continue;
                    var title = GetTitle(tr);
                    if (title == null)
                        dangling.Add($"阶级序列[{l.id}].orderedTitleRefs → 头衔 '{tr}'");
                    else if (title.kind != ETitleKind.RankTitle)
                        errors.Add($"阶级序列[{l.id}] 含非阶级头衔 '{tr}'（kind 应为 RankTitle）");
                    if (!seen.Add(tr))
                        errors.Add($"阶级序列[{l.id}] 重复头衔 '{tr}'");
                }
            }

            if (dangling.Count > 0)
                errors.Add("存在悬空引用：" + string.Join("；", dangling));

            return errors.Count == 0;
        }

        private void AddIfDanglingCharacter(string ownerId, string field, string refId, HashSet<string> dangling)
        {
            if (!string.IsNullOrEmpty(refId) && GetCharacter(refId) == null)
                dangling.Add($"角色[{ownerId}].{field} → 角色 '{refId}'");
        }

        private static void CheckDuplicates<T>(List<T> list, Func<T, string> keyOf, string label, List<string> errors)
        {
            if (list == null) return;
            var seen = new HashSet<string>();
            var dup  = new HashSet<string>();
            foreach (var e in list)
            {
                if (e == null) continue;
                var key = keyOf(e);
                if (string.IsNullOrWhiteSpace(key)) continue;
                if (!seen.Add(key)) dup.Add(key);
            }
            if (dup.Count > 0)
                errors.Add($"存在重复的 {label}：" + string.Join(", ", dup));
        }

        /// <summary>转职树是否存在环（沿 childProfessionRefs 有向边 DFS 检测回边）。</summary>
        private static bool ProfessionTreeHasCycle(ProfessionTree tree)
        {
            if (tree == null || tree.nodes == null) return false;
            var adj = new Dictionary<string, List<string>>();
            foreach (var n in tree.nodes)
            {
                if (n == null || string.IsNullOrEmpty(n.professionRef)) continue;
                if (!adj.TryGetValue(n.professionRef, out var list)) { list = new List<string>(); adj[n.professionRef] = list; }
                if (n.childProfessionRefs != null) list.AddRange(n.childProfessionRefs);
            }
            var state = new Dictionary<string, int>(); // 缺省/0=未访问，1=在栈上，2=已完成
            foreach (var key in adj.Keys)
                if (HasCycleDfs(key, adj, state)) return true;
            return false;
        }

        private static bool HasCycleDfs(string node, Dictionary<string, List<string>> adj, Dictionary<string, int> state)
        {
            state.TryGetValue(node, out int s);
            if (s == 1) return true;   // 回边 → 成环
            if (s == 2) return false;
            state[node] = 1;
            if (adj.TryGetValue(node, out var children))
                foreach (var c in children)
                    if (!string.IsNullOrEmpty(c) && HasCycleDfs(c, adj, state)) return true;
            state[node] = 2;
            return false;
        }

        #endregion

        #region 深拷贝（模板支持）

        /// <summary>用另一个数据库的全部数据深拷贝覆盖自身（新建数据文件「使用模板」时调用）。</summary>
        public void CloneFrom(ChronicleDatabase source)
        {
            if (!source) return;
            enumTypes          = source.enumTypes.Select(e => e.Clone()).ToList();
            tags               = source.tags.Select(t => t.Clone()).ToList();
            coreAttributes     = source.coreAttributes.Select(a => a.Clone()).ToList();
            traits             = source.traits.Select(t => t.Clone()).ToList();
            characterTemplates = source.characterTemplates.Select(t => t.Clone()).ToList();
            characters         = source.characters.Select(c => c.Clone()).ToList();
            coreAttributeTemplates = source.coreAttributeTemplates.Select(t => t.Clone()).ToList();
            traitTemplates         = source.traitTemplates.Select(t => t.Clone()).ToList();
            groupTags              = source.groupTags.Select(t => t.Clone()).ToList();
            numberFormatConfigs    = source.numberFormatConfigs.Select(c => c.Clone()).ToList();
            skillTemplates         = source.skillTemplates.Select(t => t.Clone()).ToList();
            skills                 = source.skills.Select(s => s.Clone()).ToList();
            professions            = source.professions.Select(p => p.Clone()).ToList();
            professionTrees        = source.professionTrees.Select(t => t.Clone()).ToList();
            titles                 = source.titles.Select(t => t.Clone()).ToList();
            rankLadders            = source.rankLadders.Select(l => l.Clone()).ToList();
        }

        #endregion
    }
}
