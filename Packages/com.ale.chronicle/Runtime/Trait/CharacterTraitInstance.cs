using System;

namespace Ale.Chronicle
{
    /// <summary>
    /// 角色身上某特质的<b>实例</b>（区别于 <see cref="TraitDefinition"/> 这个共享定义）：
    /// 记录该特质在此角色上的剩余时长、叠加层数与来源。永久特质 <see cref="remainingDays"/> 取负数（约定 -1）。
    /// </summary>
    [Serializable]
    public struct CharacterTraitInstance
    {
        /// <summary>指向 <see cref="TraitDefinition.id"/>。</summary>
        public string traitRef;

        /// <summary>剩余存活天数（世界日）；<c>&lt; 0</c>（约定 -1）表示永久、不过期。</summary>
        public float remainingDays;

        /// <summary>叠加层数（最小 1）。</summary>
        public int stacks;

        /// <summary>来源标识（如 <c>birth</c> / <c>event:瘟疫</c>；用于成组撤销与追溯）。</summary>
        public string sourceTag;

        public CharacterTraitInstance(string traitRef, float remainingDays = -1f, int stacks = 1, string sourceTag = null)
        {
            this.traitRef      = traitRef;
            this.remainingDays = remainingDays;
            this.stacks        = stacks < 1 ? 1 : stacks;
            this.sourceTag     = sourceTag;
        }

        /// <summary>永久（不倒计时）。</summary>
        public bool IsPermanent => remainingDays < 0f;

        /// <summary>已到期（临时且剩余 ≤ 0）。永久特质恒 false。</summary>
        public bool IsExpired => !IsPermanent && remainingDays <= 0f;

        /// <summary>
        /// 推进 <paramref name="days"/> 天后的新实例（值语义，返回副本）。永久特质与非正推进原样返回；
        /// 临时特质剩余天数递减且不低于 0。
        /// </summary>
        public CharacterTraitInstance Ticked(float days)
        {
            if (IsPermanent || days <= 0f) return this;
            var c = this;
            c.remainingDays = remainingDays - days;
            if (c.remainingDays < 0f) c.remainingDays = 0f;
            return c;
        }
    }
}
