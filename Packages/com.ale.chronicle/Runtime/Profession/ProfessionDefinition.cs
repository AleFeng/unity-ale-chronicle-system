using System;
using System.Collections.Generic;
using Ale.Toolkit.Runtime;
using Ale.Condition;

namespace Ale.Chronicle
{
    /// <summary>
    /// 职业定义（战士 / 法师 / 盗贼 / 医师…）：角色属性汇流的<b>加性来源之一</b>。
    /// 每级成长经 <see cref="CollectGrowthModifiers"/> 产出带 <c>prof:{id}:growth</c> 来源标记的
    /// <see cref="ModifierDefinition"/>，汇入 <see cref="CoreAttributeResolver"/>，与特质同法。
    ///
    /// <para>面向玩家的文本 / 资源字段用 <see cref="AttributeValue"/>（Text 走本地化、Sprite 走 Addressable）；
    /// <see cref="id"/> 与各 <c>*Ref</c> 是稳定键，保持裸 <see cref="string"/>。定长强类型字段之外，
    /// 另经 <see cref="templateRef"/> 承载模板 schema 驱动的自定义属性值 <see cref="values"/>（继承 <see cref="AttributeOwner"/>）。</para>
    /// </summary>
    [Serializable]
    public class ProfessionDefinition : AttributeOwner
    {
        /// <summary>来源标记前缀（<c>prof:</c>）。</summary>
        public const string SourceTagPrefix = "prof:";

        /// <summary>稳定引用键（如 "warrior"/"战士"）。</summary>
        public string id;

        /// <summary>显示名（Text）。</summary>
        public AttributeValue displayName = new AttributeValue(EFieldType.Text);

        /// <summary>说明（Text）。</summary>
        public AttributeValue description = new AttributeValue(EFieldType.Text);

        /// <summary>图标（Sprite：经 Addressable 统一收集）。</summary>
        public AttributeValue icon = new AttributeValue(EFieldType.Sprite);

        /// <summary>来源职业模板名称（可为空；决定中列过滤维与自定义字段 schema）。</summary>
        public string templateRef;

        /// <summary>职业分组 id（→ 统一分组标签池 <see cref="ChronicleDatabase.GroupTags"/> 的 id）。</summary>
        public string groupTagRef;

        /// <summary>等级上限。</summary>
        public int maxLevel = 10;

        /// <summary>升级所需经验曲线（三模式）。</summary>
        public ExpCurve expCurve = new ExpCurve();

        /// <summary>每级对核心属性的成长。</summary>
        public List<LevelGrowthEntry> growth = new List<LevelGrowthEntry>();

        /// <summary>特定等级解锁特质 / 头衔资格。</summary>
        public List<LevelUnlock> unlocks = new List<LevelUnlock>();

        /// <summary>从业 / 转职门槛（Condition System，内联可配；空 = 无门槛）。</summary>
        public ConditionExpression requirements = new ConditionExpression();

        /// <summary>允许种族（空 = 全部）。种族系统未落地前存为不透明串、宽松校验。</summary>
        public List<string> allowedRaceRefs = new List<string>();

        /// <summary>关联技能树（→ <see cref="SkillTree.id"/>；可关联一个或多个；空 = 无）。</summary>
        public List<string> skillTreeRefs = new List<string>();

        /// <summary>来自模板 schema 的自定义属性值。</summary>
        public List<AttributeEntry> values = new List<AttributeEntry>();

        // 实现基类 AttributeOwner 的抽象属性，将 values 列表暴露给基类的懒加载字典缓存。
        protected override List<AttributeEntry> AttributeEntries => values;

        public ProfessionDefinition() { }

        public ProfessionDefinition(string id, string templateRef = null)
        {
            this.id          = id;
            this.templateRef = templateRef;
        }

        /// <summary>本职业成长的来源标记：<c>prof:{id}:growth</c>。</summary>
        public string GrowthSourceTag() => SourceTagPrefix + id + ":growth";

        /// <summary>
        /// 根据当前模板 schema 协调自定义属性值集合：新增字段追加默认值、移除已删字段、保留已存在字段值
        /// （类型 / 枚举引用变化时重置）。与其它域的 RebuildAttributes 同法。
        /// </summary>
        public void RebuildAttributes(IChronicleSchemaSource src)
        {
            if (src == null) return;
            var template = src.GetProfessionTemplate(templateRef);
            AttributeSync.Sync(values, template != null ? template.attributes : null);
            InvalidateEntryCache();
        }

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
        /// 把命中 <paramref name="targetAttributeId"/>（空 = 全部）的每级成长按 <paramref name="level"/>
        /// 折算为 <c>Add</c> 修饰器（打好 <see cref="GrowthSourceTag"/> 来源标记）追加进 <paramref name="into"/>。
        /// <b>编辑器预览与运行时求值共用此函数。</b>
        /// </summary>
        public void CollectGrowthModifiers(int level, string targetAttributeId, List<ModifierDefinition> into)
        {
            if (growth == null || into == null) return;
            bool all = string.IsNullOrEmpty(targetAttributeId);
            string src = GrowthSourceTag();
            foreach (var g in growth)
            {
                if (g == null || string.IsNullOrEmpty(g.coreAttrId)) continue;
                if (!all && g.coreAttrId != targetAttributeId) continue;
                into.Add(new ModifierDefinition(g.coreAttrId, EModifierOperation.Add, g.MagnitudeAt(level), src));
            }
        }

        /// <summary>归一：确保 Text / Sprite / 曲线字段类型正确、列表非空（编辑器绘制前 / 反序列化后调用）。</summary>
        public void Normalize()
        {
            displayName = EnsureType(displayName, EFieldType.Text);
            description = EnsureType(description, EFieldType.Text);
            icon        = EnsureType(icon,        EFieldType.Sprite);
            if (expCurve == null) expCurve = new ExpCurve();
            expCurve.Normalize();
            if (growth == null)          growth          = new List<LevelGrowthEntry>();
            if (unlocks == null)         unlocks         = new List<LevelUnlock>();
            if (requirements == null)    requirements    = new ConditionExpression();
            if (allowedRaceRefs == null) allowedRaceRefs = new List<string>();
            if (skillTreeRefs == null)   skillTreeRefs   = new List<string>();
            if (values == null)          values          = new List<AttributeEntry>();
            if (maxLevel < 1)            maxLevel        = 1;
            foreach (var g in growth) g?.Normalize();
        }

        private static AttributeValue EnsureType(AttributeValue v, EFieldType t)
        {
            if (v == null) return new AttributeValue(t);
            if (v.Type != t || v.IsArray) v.ChangeType(t, false);
            return v;
        }

        public ProfessionDefinition Clone()
        {
            var c = new ProfessionDefinition(id, templateRef)
            {
                groupTagRef = groupTagRef,
                maxLevel    = maxLevel,
                displayName = displayName != null ? displayName.Clone() : new AttributeValue(EFieldType.Text),
                description = description != null ? description.Clone() : new AttributeValue(EFieldType.Text),
                icon        = icon        != null ? icon.Clone()        : new AttributeValue(EFieldType.Sprite),
                expCurve    = expCurve    != null ? expCurve.Clone()    : new ExpCurve(),
                requirements = requirements != null ? requirements.Clone() : new ConditionExpression(),
                allowedRaceRefs = allowedRaceRefs != null ? new List<string>(allowedRaceRefs) : new List<string>(),
                skillTreeRefs   = skillTreeRefs   != null ? new List<string>(skillTreeRefs)   : new List<string>(),
            };
            if (growth != null)
                foreach (var g in growth) c.growth.Add(g != null ? g.Clone() : new LevelGrowthEntry());
            if (unlocks != null)
                foreach (var u in unlocks) c.unlocks.Add(u != null ? u.Clone() : new LevelUnlock());
            if (values != null)
                foreach (var e in values) c.values.Add(e.Clone());
            return c;
        }
    }
}
