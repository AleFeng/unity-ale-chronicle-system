using System;
using System.Collections.Generic;
using Ale.Toolkit.Runtime;
using Ale.Condition;

namespace Ale.Chronicle
{
    /// <summary>
    /// 头衔定义。两类由 <see cref="kind"/> 区分：
    /// <b>阶级头衔（RankTitle）</b>——爵位 / 军衔，逐级晋升、同时只持其一（由 <see cref="RankLadder"/> 组织成阶梯）；
    /// <b>称号（Epithet）</b>——剑圣 / 征服王，靠事迹获得、多为唯一。
    /// 对核心属性的加成经 <see cref="CollectModifiers"/> 汇入 <see cref="CoreAttributeResolver"/>（<c>title:{id}</c>），与特质同法；
    /// <see cref="opinionModifiers"/> 面向社交 / 好感轴，暂只存储、<b>不</b>汇入核心属性。
    ///
    /// <para>面向玩家的文本 / 资源字段用 <see cref="AttributeValue"/>（Text 走本地化、Sprite 走 Addressable）；
    /// <see cref="id"/> 与各 <c>*Ref</c> 是稳定键，保持裸 <see cref="string"/>。定长强类型字段，不走模板 schema。</para>
    /// </summary>
    [Serializable]
    public class TitleDefinition
    {
        /// <summary>来源标记前缀（<c>title:</c>）。</summary>
        public const string SourceTagPrefix = "title:";

        /// <summary>稳定引用键（如 "duke"/"公爵"）。</summary>
        public string id;

        /// <summary>显示名（Text）。</summary>
        public AttributeValue displayName = new AttributeValue(EFieldType.Text);

        /// <summary>说明（Text）。</summary>
        public AttributeValue description = new AttributeValue(EFieldType.Text);

        /// <summary>图标（Sprite：经 Addressable 统一收集）。</summary>
        public AttributeValue icon = new AttributeValue(EFieldType.Sprite);

        /// <summary>头衔种类（阶级头衔 / 称号）。</summary>
        public ETitleKind kind = ETitleKind.RankTitle;

        /// <summary>分组 id（→ 统一分组标签池 <see cref="ChronicleDatabase.GroupTags"/> 的 id；世俗爵位 / 教会 / 军功…）。</summary>
        public string groupTagRef;

        // —— 阶级头衔专用 ——

        /// <summary>阶梯内高低（数值越大越高；公 &gt; 侯 &gt; 伯 &gt; 子 &gt; 男）。</summary>
        public int rankTier;

        /// <summary>可继承。</summary>
        public bool heritable;

        /// <summary>继承法引用（继承系统未落地前存为不透明串）。</summary>
        public string successionPolicyRef;

        // —— 称号专用 ——

        /// <summary>全世界唯一持有者（授予时从他人剥夺）。</summary>
        public bool isUnique;

        /// <summary>获得条件（Condition System，内联可配；空 = 无门槛）。</summary>
        public ConditionExpression acquisitionConditions = new ConditionExpression();

        /// <summary>可被剥夺。</summary>
        public bool isRevocable = true;

        // —— 通用：对属性 / 好感的影响 ——

        /// <summary>对核心属性的修饰器（toolkit 类型；汇入 <see cref="CoreAttributeResolver"/>，来源 <c>title:{id}</c>）。</summary>
        public List<ModifierDefinition> modifiers = new List<ModifierDefinition>();

        /// <summary>对好感 / 观感的修饰器（社交轴；暂只存储、不汇入核心属性）。</summary>
        public List<ModifierDefinition> opinionModifiers = new List<ModifierDefinition>();

        public TitleDefinition() { }

        public TitleDefinition(string id) { this.id = id; }

        /// <summary>本头衔作为修饰器来源的标记（<c>title:{id}</c>）。</summary>
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
        /// 把本头衔中作用于 <paramref name="targetAttributeId"/>（空 = 全部）的 <see cref="modifiers"/><b>克隆</b>追加进
        /// <paramref name="into"/>；克隆件若无自带 <see cref="ModifierDefinition.sourceTag"/> 则打上 <see cref="SourceTag"/>。
        /// <b><see cref="opinionModifiers"/> 不在此收集</b>（面向社交轴，非核心属性汇流）。
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

        /// <summary>归一：确保 Text / Sprite 字段类型正确、列表非空、条件非空。</summary>
        public void Normalize()
        {
            displayName = EnsureType(displayName, EFieldType.Text);
            description = EnsureType(description, EFieldType.Text);
            icon        = EnsureType(icon,        EFieldType.Sprite);
            if (acquisitionConditions == null) acquisitionConditions = new ConditionExpression();
            if (modifiers == null)             modifiers             = new List<ModifierDefinition>();
            if (opinionModifiers == null)      opinionModifiers      = new List<ModifierDefinition>();
        }

        private static AttributeValue EnsureType(AttributeValue v, EFieldType t)
        {
            if (v == null) return new AttributeValue(t);
            if (v.Type != t || v.IsArray) v.ChangeType(t, false);
            return v;
        }

        public TitleDefinition Clone()
        {
            var c = new TitleDefinition
            {
                id                    = id,
                kind                  = kind,
                groupTagRef           = groupTagRef,
                rankTier              = rankTier,
                heritable             = heritable,
                successionPolicyRef   = successionPolicyRef,
                isUnique              = isUnique,
                isRevocable           = isRevocable,
                displayName           = displayName != null ? displayName.Clone() : new AttributeValue(EFieldType.Text),
                description           = description != null ? description.Clone() : new AttributeValue(EFieldType.Text),
                icon                  = icon        != null ? icon.Clone()        : new AttributeValue(EFieldType.Sprite),
                acquisitionConditions = acquisitionConditions != null ? acquisitionConditions.Clone() : new ConditionExpression(),
            };
            if (modifiers != null)
                foreach (var m in modifiers) c.modifiers.Add(m != null ? m.Clone() : null);
            if (opinionModifiers != null)
                foreach (var m in opinionModifiers) c.opinionModifiers.Add(m != null ? m.Clone() : null);
            return c;
        }
    }
}
