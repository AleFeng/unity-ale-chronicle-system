using System;
using System.Collections.Generic;
using Ale.Toolkit.Runtime;
using Ale.Condition;

namespace Ale.Chronicle
{
    /// <summary>
    /// 特质定义（勇敢 / 贪婪 / 天生神力 / 骨折…）：角色属性汇流的<b>第一个修饰器来源</b>。
    /// 一条特质携带若干 <see cref="ModifierDefinition"/>（toolkit 类型），经 <see cref="TraitDefinition.SourceTag"/>
    /// 打上来源标记后汇入 <see cref="CoreAttributeResolver"/>，形成「基础 → 当前 + 逐来源明细」。
    ///
    /// <para>面向玩家的文本 / 资源字段用 <see cref="AttributeValue"/>（Text 走本地化、Sprite 走 Addressable）；
    /// <see cref="id"/> 与各 <c>*Ref</c> 是稳定键，保持裸 <see cref="string"/>。</para>
    ///
    /// <para><b>硬互斥</b>由 <see cref="groupEquivalenceRef"/>（同组只可持其一）与 <see cref="incompatibleTraitRefs"/> 表达；
    /// <b>软相性</b>由 <see cref="compatibilities"/> 表达（只影响社交好感、不阻止并存）。
    /// <see cref="eligibility"/> 是获得该特质需满足的条件（Condition System，内联可配）。</para>
    ///
    /// <para><b>Effect 推迟</b>：<c>onGained/onLost</c> 等突变随 Effect 系统落地，本阶段不建模。</para>
    /// </summary>
    [Serializable]
    public class TraitDefinition
    {
        /// <summary>来源标记前缀（<c>trait:</c>）。</summary>
        public const string SourceTagPrefix = "trait:";

        /// <summary>稳定引用键（如 "brave"/"勇敢"）。</summary>
        public string id;

        /// <summary>显示名（Text）。</summary>
        public AttributeValue displayName = new AttributeValue(EFieldType.Text);

        /// <summary>说明（Text）。</summary>
        public AttributeValue description = new AttributeValue(EFieldType.Text);

        /// <summary>图标（Sprite：经 Addressable 统一收集）。</summary>
        public AttributeValue icon = new AttributeValue(EFieldType.Sprite);

        /// <summary>时效：永久 / 临时。</summary>
        public ETraitLifetime lifetime = ETraitLifetime.Permanent;

        /// <summary>临时特质的默认存活天数（<see cref="ETraitLifetime.Temporary"/> 生效）。</summary>
        public float defaultDurationDays;

        /// <summary>临时特质重复获得时刷新剩余时长（true）还是各自计时（false）。</summary>
        public bool durationStacksRefresh = true;

        /// <summary>特质类别（EnumType 名，可选：性格 / 后天 / 疾病…）。</summary>
        public string categoryEnumRef;

        /// <summary>互斥组：同一 <see cref="groupEquivalenceRef"/> 的特质彼此互斥（角色只可持其一）。空 = 不参与分组互斥。</summary>
        public string groupEquivalenceRef;

        /// <summary>显式互斥特质 id 列表（与 <see cref="groupEquivalenceRef"/> 叠加，双向生效）。</summary>
        public List<string> incompatibleTraitRefs = new List<string>();

        /// <summary>携带的修饰器（toolkit 类型；汇入 <see cref="CoreAttributeResolver"/>）。</summary>
        public List<ModifierDefinition> modifiers = new List<ModifierDefinition>();

        /// <summary>关联功能标签（指向 toolkit <see cref="Tag"/> 的名，可携带扩展 schema 字段）。</summary>
        public string functionTagRef;

        /// <summary>软相性（对拥有某特质者的好感增减）。</summary>
        public List<TraitCompatibility> compatibilities = new List<TraitCompatibility>();

        /// <summary>是否可遗传。</summary>
        public bool genetic;

        /// <summary>父母携带时遗传给子女的概率 [0,1]（<see cref="genetic"/> 生效）。</summary>
        public float inheritChance;

        /// <summary>出生自带（先天）概率 [0,1]。</summary>
        public float birthChance;

        /// <summary>AI 行为轴权重偏移。</summary>
        public List<TraitAiWeight> aiWeights = new List<TraitAiWeight>();

        /// <summary>获得该特质需满足的条件（Condition System；空 = 无门槛）。</summary>
        public ConditionExpression eligibility = new ConditionExpression();

        public TraitDefinition() { }

        public TraitDefinition(string id) { this.id = id; }

        /// <summary>临时特质。</summary>
        public bool IsTemporary => lifetime == ETraitLifetime.Temporary;

        /// <summary>本特质作为修饰器来源的标记（<c>trait:{id}</c>）。</summary>
        public string SourceTag() => SourceTagPrefix + id;

        /// <summary>显示名解析（本地化优先 → 纯文本，均空回退 <see cref="id"/>）。运行时 UI 用。</summary>
        public string ResolveDisplayName()
        {
            string s = displayName != null ? displayName.ResolveText() : null;
            return !string.IsNullOrEmpty(s) ? s : id;
        }

        /// <summary>纯文本显示名（编辑期稳定，均空回退 <see cref="id"/>）。编辑器列表 / 下拉用。</summary>
        public string PlainName()
        {
            string s = displayName != null ? displayName.GetTextValue() : null;
            return !string.IsNullOrEmpty(s) ? s : id;
        }

        /// <summary>
        /// 把本特质中作用于 <paramref name="targetAttributeId"/> 的修饰器<b>克隆</b>追加进 <paramref name="into"/>；
        /// 传空 <paramref name="targetAttributeId"/> 表示收集全部。克隆件若无自带 <see cref="ModifierDefinition.sourceTag"/>，
        /// 则打上 <see cref="SourceTag"/>（保证明细可按特质追溯、可成组撤销），配置本体不被改动。
        /// </summary>
        public void CollectModifiers(string targetAttributeId, List<ModifierDefinition> into)
        {
            if (modifiers == null || into == null) return;
            string src = SourceTag();
            bool all = string.IsNullOrEmpty(targetAttributeId);
            foreach (var m in modifiers)
            {
                if (m == null) continue;
                if (!all && m.targetAttributeId != targetAttributeId) continue;
                var c = m.Clone();
                if (string.IsNullOrEmpty(c.sourceTag)) c.sourceTag = src;
                into.Add(c);
            }
        }

        /// <summary>
        /// 本特质是否与 <paramref name="other"/> 硬互斥：同一非空 <see cref="groupEquivalenceRef"/>，
        /// 或任一方的 <see cref="incompatibleTraitRefs"/> 列出对方。自身 / null 恒 false。
        /// </summary>
        public bool IsIncompatibleWith(TraitDefinition other)
        {
            if (other == null || ReferenceEquals(other, this)) return false;
            if (!string.IsNullOrEmpty(groupEquivalenceRef) && groupEquivalenceRef == other.groupEquivalenceRef) return true;
            if (incompatibleTraitRefs != null && !string.IsNullOrEmpty(other.id) && incompatibleTraitRefs.Contains(other.id)) return true;
            if (other.incompatibleTraitRefs != null && !string.IsNullOrEmpty(id) && other.incompatibleTraitRefs.Contains(id)) return true;
            return false;
        }

        /// <summary>归一：确保 Text / Sprite 字段类型正确（修旧数据 / 防漂移；编辑器绘制前调用）。</summary>
        public void Normalize()
        {
            displayName = EnsureType(displayName, EFieldType.Text);
            description = EnsureType(description, EFieldType.Text);
            icon        = EnsureType(icon,        EFieldType.Sprite);
            if (incompatibleTraitRefs == null) incompatibleTraitRefs = new List<string>();
            if (modifiers == null)             modifiers             = new List<ModifierDefinition>();
            if (compatibilities == null)       compatibilities       = new List<TraitCompatibility>();
            if (aiWeights == null)             aiWeights             = new List<TraitAiWeight>();
            if (eligibility == null)           eligibility           = new ConditionExpression();
        }

        private static AttributeValue EnsureType(AttributeValue v, EFieldType t)
        {
            if (v == null) return new AttributeValue(t);
            if (v.Type != t || v.IsArray) v.ChangeType(t, false);
            return v;
        }

        public TraitDefinition Clone()
        {
            var c = new TraitDefinition
            {
                id                    = id,
                displayName           = displayName != null ? displayName.Clone() : new AttributeValue(EFieldType.Text),
                description           = description != null ? description.Clone() : new AttributeValue(EFieldType.Text),
                icon                  = icon        != null ? icon.Clone()        : new AttributeValue(EFieldType.Sprite),
                lifetime              = lifetime,
                defaultDurationDays   = defaultDurationDays,
                durationStacksRefresh = durationStacksRefresh,
                categoryEnumRef       = categoryEnumRef,
                groupEquivalenceRef   = groupEquivalenceRef,
                functionTagRef        = functionTagRef,
                genetic               = genetic,
                inheritChance         = inheritChance,
                birthChance           = birthChance,
                eligibility           = eligibility != null ? eligibility.Clone() : new ConditionExpression(),
            };
            c.incompatibleTraitRefs = incompatibleTraitRefs != null ? new List<string>(incompatibleTraitRefs) : new List<string>();
            c.compatibilities       = compatibilities       != null ? new List<TraitCompatibility>(compatibilities) : new List<TraitCompatibility>();
            c.aiWeights             = aiWeights             != null ? new List<TraitAiWeight>(aiWeights) : new List<TraitAiWeight>();
            c.modifiers             = new List<ModifierDefinition>(modifiers != null ? modifiers.Count : 0);
            if (modifiers != null)
                foreach (var m in modifiers) c.modifiers.Add(m != null ? m.Clone() : null);
            return c;
        }
    }
}
