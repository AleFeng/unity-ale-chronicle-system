using System;
using Ale.Toolkit.Runtime;

namespace Ale.Chronicle
{
    /// <summary>
    /// 某核心属性随职业等级的成长。线性 <see cref="perLevel"/> 与曲线 <see cref="levelCurve"/> 二选一
    /// （<see cref="useCurve"/> 切换）。<see cref="MagnitudeAt"/> 给出某等级的累计成长值，
    /// 由 <see cref="ProfessionDefinition.CollectGrowthModifiers"/> 折算为 <c>Add</c> 修饰器汇入核心属性。
    /// </summary>
    [Serializable]
    public class LevelGrowthEntry
    {
        /// <summary>目标核心属性 id（→ <see cref="CoreAttributeDefinition.id"/>）。</summary>
        public string coreAttrId;

        /// <summary>线性：每级增量（战士每级 力量+2）。</summary>
        public float perLevel;

        /// <summary>是否改用曲线表达成长。</summary>
        public bool useCurve;

        /// <summary>曲线：<c>level → 累计成长</c>（<see cref="useCurve"/> 生效）。</summary>
        public AttributeValue levelCurve = new AttributeValue(EFieldType.AnimationCurve);

        /// <summary>在 <paramref name="level"/> 级时该属性的累计成长值。线性取 <c>perLevel × (level-1)</c>
        /// —— 1 级刚转职成长为 0，逐级累加。</summary>
        public float MagnitudeAt(int level)
        {
            if (useCurve)
            {
                var c = levelCurve != null ? levelCurve.GetAnimationCurve(0) : null;
                return c == null ? 0f : c.Evaluate(level);
            }
            return perLevel * (level - 1);
        }

        public void Normalize()
        {
            if (levelCurve == null) levelCurve = new AttributeValue(EFieldType.AnimationCurve);
            else if (levelCurve.Type != EFieldType.AnimationCurve || levelCurve.IsArray)
                levelCurve.ChangeType(EFieldType.AnimationCurve, false);
        }

        public LevelGrowthEntry Clone() => new LevelGrowthEntry
        {
            coreAttrId = coreAttrId,
            perLevel   = perLevel,
            useCurve   = useCurve,
            levelCurve = levelCurve != null ? levelCurve.Clone() : new AttributeValue(EFieldType.AnimationCurve),
        };
    }
}
