using System;
using System.Collections.Generic;
using Ale.Toolkit.Runtime;

namespace Ale.Chronicle
{
    /// <summary>
    /// 角色定义：编年史系统的核心实体。<b>组合装配</b>三部分——
    /// ① 身份自由字段 <see cref="values"/>（姓名 / 生日 / 性别…，schema 来自模板 ∪ 特质携带的功能标签，由 <see cref="RebuildAttributes"/> 对账）；
    /// ② 核心属性基础值 <see cref="coreAttributes"/>（当前值由 <see cref="CoreAttributeResolver"/> 汇流求出）；
    /// ③ 特质实例 <see cref="traits"/>（属性汇流的第一个修饰器来源）。
    /// 外加家族指针。种族 / 职业 / 社交 / 头衔 / 健康 / 文化字段留待各子系统落地（见下方 TODO）。
    ///
    /// <para>继承 <see cref="AttributeOwner"/> 复用带缓存的 <see cref="AttributeOwner.GetEntry"/> 与
    /// 支持本地化的 <see cref="AttributeOwner.GetAttributeValue{T}"/>。</para>
    /// </summary>
    [Serializable]
    public class CharacterDefinition : AttributeOwner
    {
        /// <summary>唯一标识（角色列表去重；家族 / 社交等按此 id 相互引用）。</summary>
        public string id;

        /// <summary>来源角色模板名称（决定身份自由字段 schema）。</summary>
        public string templateRef;

        /// <summary>身份自由字段值（schema = 模板 ∪ 特质功能标签；<see cref="RebuildAttributes"/> 对账）。</summary>
        public List<AttributeEntry> values = new List<AttributeEntry>();

        /// <summary>核心属性基础值。</summary>
        public List<CoreAttributeValue> coreAttributes = new List<CoreAttributeValue>();

        /// <summary>特质实例。</summary>
        public List<CharacterTraitInstance> traits = new List<CharacterTraitInstance>();

        // ── 家族指针 ───────────────────────────────────────────────────────────────

        /// <summary>父亲角色 id（可空）。</summary>
        public string fatherRef;

        /// <summary>母亲角色 id（可空）。</summary>
        public string motherRef;

        /// <summary>子女角色 id 列表。</summary>
        public List<string> childRefs = new List<string>();

        /// <summary>角色起始职业配装（等级 / 经验）。运行时可变进度由 <c>ProfessionRuntimeManager</c> 持有。</summary>
        public List<CharacterProfession> professions = new List<CharacterProfession>();

        /// <summary>角色起始头衔配装。运行时持有由 <c>TitleRuntimeManager</c> 持有。</summary>
        public List<CharacterTitle> titles = new List<CharacterTitle>();

        // TODO(子系统)：种族 / 社交(关系条目) / 健康(多部位·能力层) / 文化 等字段留空占位，
        //   随各自子系统作为「产出 Modifier 汇入同一主干」的加性切片落地。
        //   职业成长 + 头衔加成的 CollectModifiers 汇流在 Step 6 落地。

        // 实现 AttributeOwner：把 values 暴露给基类懒加载字典缓存。
        protected override List<AttributeEntry> AttributeEntries => values;

        public CharacterDefinition() { }

        public CharacterDefinition(string id, string templateRef = null)
        {
            this.id          = id;
            this.templateRef = templateRef;
        }

        // ── schema 对账 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 依「模板自有字段 ∪ 各特质携带的功能标签字段」协调 <see cref="values"/>：
        /// 追加新增字段的默认值条目、移除已不属于任何来源的条目、已存在字段保留原值
        /// （类型 / 数组形态 / 枚举引用漂移时重置），并按来源顺序重排。
        /// <para>顺序：① 模板字段（最高优先级）；② 按 <see cref="traits"/> 顺序的特质功能标签字段。</para>
        /// </summary>
        public void RebuildAttributes(IChronicleSchemaSource src)
        {
            if (src == null) return;

            var desired    = new List<AttributeDefinition>();
            var desiredIds = new HashSet<string>();

            // ① 模板自有字段
            var template = src.GetCharacterTemplate(templateRef);
            if (template != null)
                CollectDefinitions(template.attributes, desired, desiredIds);

            // ② 特质携带的功能标签字段（按特质顺序，去重）
            foreach (var ti in traits)
            {
                var trait = src.GetTrait(ti.traitRef);
                if (trait == null || string.IsNullOrEmpty(trait.functionTagRef)) continue;
                var tag = src.GetTag(trait.functionTagRef);
                if (tag != null)
                    CollectDefinitions(tag.attributes, desired, desiredIds);
            }

            AttributeSync.Sync(values, desired, reorder: true);
            InvalidateEntryCache();
        }

        private static void CollectDefinitions(List<AttributeDefinition> source,
            List<AttributeDefinition> desired, HashSet<string> desiredIds)
        {
            if (source == null) return;
            foreach (var def in source)
            {
                if (def == null || string.IsNullOrEmpty(def.id)) continue;
                if (desiredIds.Add(def.id)) desired.Add(def);
            }
        }

        // ── 属性汇流（组合求值的高层入口在 CoreAttributeResolver.Evaluate(character, def, src)）──

        /// <summary>取某核心属性的基础值；角色未显式配置则回退 <paramref name="def"/> 的默认基础值（无 def 则 0）。</summary>
        public float GetCoreBaseValue(string attrId, CoreAttributeDefinition def = null)
        {
            if (!string.IsNullOrEmpty(attrId))
                foreach (var cv in coreAttributes)
                    if (cv.attrId == attrId) return cv.baseValue;
            return def != null ? def.defaultBase : 0f;
        }

        /// <summary>
        /// 把全部特质中作用于 <paramref name="targetAttributeId"/> 的修饰器（克隆、打好来源标记）追加进
        /// <paramref name="into"/>；传空 <paramref name="targetAttributeId"/> 收集全部。特质定义经 <paramref name="src"/> 反查。
        /// </summary>
        public void CollectModifiers(string targetAttributeId, IChronicleSchemaSource src, List<ModifierDefinition> into)
        {
            if (src == null || into == null) return;
            foreach (var ti in traits)
            {
                var trait = src.GetTrait(ti.traitRef);
                trait?.CollectModifiers(targetAttributeId, into);
            }
        }

        // ── 年龄 ───────────────────────────────────────────────────────────────────

        /// <summary>
        /// 年龄 = 当前世界日 − 出生世界日（读 <see cref="WellKnownAttr.Birthday"/>；缺失按 0 出生日），
        /// 单位为<b>世界日</b>，负数夹到 0。「日 → 岁」的换算依赖历法 / 种族，随子系统落地。
        /// </summary>
        public int GetAge(int worldDay)
        {
            int birth = GetAttributeValue<int>(WellKnownAttr.Birthday, 0);
            int age = worldDay - birth;
            return age < 0 ? 0 : age;
        }

        // TODO(种族系统)：GetCurrentAgeStage —— 年龄阶段（婴幼 / 少年 / 成年 / 老年…）的划分依赖种族，
        //   随种族子系统落地。此处暂不实现，避免过早绑定阶段模型。

        // ── 深拷贝 ─────────────────────────────────────────────────────────────────

        public CharacterDefinition Clone()
        {
            var clone = new CharacterDefinition(id, templateRef)
            {
                fatherRef = fatherRef,
                motherRef = motherRef,
                childRefs = new List<string>(childRefs),
                coreAttributes = new List<CoreAttributeValue>(coreAttributes),
                traits = new List<CharacterTraitInstance>(traits),
                professions = new List<CharacterProfession>(professions),
                titles = new List<CharacterTitle>(titles),
            };
            foreach (var e in values)
                clone.values.Add(e != null ? e.Clone() : new AttributeEntry());
            return clone;
        }
    }
}
