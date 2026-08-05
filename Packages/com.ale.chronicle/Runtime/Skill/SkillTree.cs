using System;
using System.Collections.Generic;
using Ale.Toolkit.Runtime;
using Ale.Condition;

namespace Ale.Chronicle
{
    /// <summary>技能树类型（一个技能树只属一种）。</summary>
    public enum ESkillTreeKind
    {
        /// <summary>列表：无层级 / 先后，一组技能打包；每技能可单独配解锁条件。</summary>
        List = 0,

        /// <summary>层级：先分层、层内加技能；层级与技能均可配解锁条件（通常配在层级上）。</summary>
        Tiered = 1,

        /// <summary>树状：每技能以一或多个前置技能（AND）作解锁；每技能可再单独配解锁条件。</summary>
        Tree = 2,
    }

    /// <summary>技能点获取方式（点数发放的时机 / 规则）。</summary>
    public enum ESkillPointGrantMode
    {
        /// <summary>一次性达成即得：条件首次满足时发放一次。</summary>
        OnceOnReached = 0,

        /// <summary>持续生效：条件成立期间提供这些点，条件不再成立则收回。</summary>
        WhileActive = 1,

        /// <summary>每级 / 可重复：条件每次达成或每提升一级发放一次（按等级线性累加）。</summary>
        PerLevelRepeatable = 2,
    }

    /// <summary>技能树中的一个技能条目（列表成员 / 层级成员 / 树状节点，三类型共用）。</summary>
    [Serializable]
    public class SkillTreeEntry
    {
        /// <summary>引用的技能 id（→ <see cref="Skill.id"/>）。</summary>
        public string skillRef;

        /// <summary>该技能的解锁条件（Condition System，内联可配；空 = 无门槛）。</summary>
        public ConditionExpression unlockCondition = new ConditionExpression();

        /// <summary>所属层级 key（仅 Tiered 用；→ <see cref="SkillTreeTier.key"/>；其它类型空）。</summary>
        public string tierKey;

        /// <summary>前置技能（仅 Tree 用；AND 语义，全部前置习得才解锁；其它类型空）。</summary>
        public List<string> prerequisiteSkillRefs = new List<string>();

        public void Normalize()
        {
            if (unlockCondition == null)       unlockCondition       = new ConditionExpression();
            if (prerequisiteSkillRefs == null) prerequisiteSkillRefs = new List<string>();
        }

        public SkillTreeEntry Clone() => new SkillTreeEntry
        {
            skillRef              = skillRef,
            unlockCondition       = unlockCondition != null ? unlockCondition.Clone() : new ConditionExpression(),
            tierKey               = tierKey,
            prerequisiteSkillRefs = prerequisiteSkillRefs != null ? new List<string>(prerequisiteSkillRefs) : new List<string>(),
        };
    }

    /// <summary>技能树的一个层级（仅 Tiered 用；顺序 = 列表序）。</summary>
    [Serializable]
    public class SkillTreeTier
    {
        /// <summary>层级稳定 key（技能以 <see cref="SkillTreeEntry.tierKey"/> 关联；重排层级不失联）。</summary>
        public string key;

        /// <summary>层级显示名（Text）。</summary>
        public AttributeValue displayName = new AttributeValue(EFieldType.Text);

        /// <summary>层级解锁条件（Condition System，内联可配；空 = 无门槛）。</summary>
        public ConditionExpression unlockCondition = new ConditionExpression();

        public void Normalize()
        {
            displayName = EnsureText(displayName);
            if (unlockCondition == null) unlockCondition = new ConditionExpression();
        }

        public SkillTreeTier Clone() => new SkillTreeTier
        {
            key             = key,
            displayName     = displayName != null ? displayName.Clone() : new AttributeValue(EFieldType.Text),
            unlockCondition = unlockCondition != null ? unlockCondition.Clone() : new ConditionExpression(),
        };

        private static AttributeValue EnsureText(AttributeValue v)
        {
            if (v == null) return new AttributeValue(EFieldType.Text);
            if (v.Type != EFieldType.Text || v.IsArray) v.ChangeType(EFieldType.Text, false);
            return v;
        }
    }

    /// <summary>技能点获取条目：满足 <see cref="condition"/> 时按 <see cref="mode"/> 发放 <see cref="points"/> 点。</summary>
    [Serializable]
    public class SkillPointGrant
    {
        /// <summary>发放点数。</summary>
        public int points;

        /// <summary>获取条件（Condition System，一般为某职业等级；空 = 无条件）。</summary>
        public ConditionExpression condition = new ConditionExpression();

        /// <summary>获取方式（点数发放的时机 / 规则）。</summary>
        public ESkillPointGrantMode mode = ESkillPointGrantMode.OnceOnReached;

        public void Normalize()
        {
            if (condition == null) condition = new ConditionExpression();
        }

        public SkillPointGrant Clone() => new SkillPointGrant
        {
            points    = points,
            condition = condition != null ? condition.Clone() : new ConditionExpression(),
            mode      = mode,
        };
    }

    /// <summary>
    /// 一棵命名技能树：把一组技能按 <see cref="kind"/> 组织——列表 / 层级 / 树状。每技能（及层级）可单独配解锁条件；
    /// 另配多个技能点获取条目 <see cref="pointGrants"/>。由职业经 <c>skillTreeRefs</c> 关联；不参与角色属性汇流，纯组织 + 解锁约束。
    /// <para>条件承载体一律铺平为本树下的一层数组（<see cref="skills"/> / <see cref="tiers"/> / <see cref="pointGrants"/>），
    /// 层级与技能不做物理嵌套（技能以 <see cref="SkillTreeEntry.tierKey"/> 关联层级 → 重排层级不失联）。</para>
    /// </summary>
    [Serializable]
    public class SkillTree
    {
        /// <summary>稳定引用键。</summary>
        public string id;

        /// <summary>显示名（Text）。</summary>
        public AttributeValue displayName = new AttributeValue(EFieldType.Text);

        /// <summary>技能树类型（列表 / 层级 / 树状）。</summary>
        public ESkillTreeKind kind = ESkillTreeKind.List;

        /// <summary>技能条目（三类型共用：列表成员 / 层级成员 / 树状节点）。</summary>
        public List<SkillTreeEntry> skills = new List<SkillTreeEntry>();

        /// <summary>层级（仅 Tiered 用；顺序 = 列表序）。</summary>
        public List<SkillTreeTier> tiers = new List<SkillTreeTier>();

        /// <summary>技能点获取条目（所有类型可配）。</summary>
        public List<SkillPointGrant> pointGrants = new List<SkillPointGrant>();

        public SkillTree() { }

        public SkillTree(string id) { this.id = id; }

        /// <summary>根技能（仅 Tree 语义）= 前置为空的技能条目 skillRef。</summary>
        public IEnumerable<string> Roots()
        {
            if (skills == null) yield break;
            foreach (var e in skills)
                if (e != null && !string.IsNullOrEmpty(e.skillRef)
                    && (e.prerequisiteSkillRefs == null || e.prerequisiteSkillRefs.Count == 0))
                    yield return e.skillRef;
        }

        /// <summary>按技能 id 查条目（未命中返回 null）。</summary>
        public SkillTreeEntry FindEntry(string skillRef)
        {
            if (string.IsNullOrEmpty(skillRef) || skills == null) return null;
            foreach (var e in skills)
                if (e != null && e.skillRef == skillRef) return e;
            return null;
        }

        /// <summary>某层级 key 下的技能条目（仅 Tiered 语义）。</summary>
        public IEnumerable<SkillTreeEntry> EntriesInTier(string tierKey)
        {
            if (skills == null) yield break;
            foreach (var e in skills)
                if (e != null && e.tierKey == tierKey) yield return e;
        }

        public void Normalize()
        {
            displayName = EnsureText(displayName);
            if (skills == null)      skills      = new List<SkillTreeEntry>();
            if (tiers == null)       tiers       = new List<SkillTreeTier>();
            if (pointGrants == null) pointGrants = new List<SkillPointGrant>();
            foreach (var e in skills)      e?.Normalize();
            foreach (var t in tiers)       t?.Normalize();
            foreach (var g in pointGrants) g?.Normalize();
        }

        public SkillTree Clone()
        {
            var c = new SkillTree
            {
                id          = id,
                displayName = displayName != null ? displayName.Clone() : new AttributeValue(EFieldType.Text),
                kind        = kind,
            };
            if (skills != null)
                foreach (var e in skills) c.skills.Add(e != null ? e.Clone() : new SkillTreeEntry());
            if (tiers != null)
                foreach (var t in tiers) c.tiers.Add(t != null ? t.Clone() : new SkillTreeTier());
            if (pointGrants != null)
                foreach (var g in pointGrants) c.pointGrants.Add(g != null ? g.Clone() : new SkillPointGrant());
            return c;
        }

        private static AttributeValue EnsureText(AttributeValue v)
        {
            if (v == null) return new AttributeValue(EFieldType.Text);
            if (v.Type != EFieldType.Text || v.IsArray) v.ChangeType(EFieldType.Text, false);
            return v;
        }
    }
}
