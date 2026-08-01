using System;

namespace Ale.Chronicle
{
    /// <summary>
    /// 特质相性（soft）：当对方角色拥有 <see cref="otherTraitRef"/> 特质时，本特质持有者对其的好感增减。
    /// 与「硬互斥」（<see cref="TraitDefinition.groupEquivalenceRef"/> / <see cref="TraitDefinition.incompatibleTraitRefs"/>）不同——
    /// 相性只影响社交好感，不阻止两特质并存。运行时由社交系统消费，本阶段仅配置。
    /// </summary>
    [Serializable]
    public struct TraitCompatibility
    {
        /// <summary>对方拥有的特质 id（指向 <see cref="TraitDefinition.id"/>）。</summary>
        public string otherTraitRef;

        /// <summary>好感增减：正 = 相吸，负 = 相斥。</summary>
        public float opinionDelta;

        public TraitCompatibility(string otherTraitRef, float opinionDelta)
        {
            this.otherTraitRef = otherTraitRef;
            this.opinionDelta   = opinionDelta;
        }
    }
}
