using System;

namespace Ale.Chronicle
{
    /// <summary>
    /// 角色实例上持有的一个职业（编辑期起始配装：等级 / 经验）。运行时可变进度由
    /// <c>ProfessionRuntimeManager</c> 持有并存档；本结构用于配置与编辑器汇流预览。
    /// </summary>
    [Serializable]
    public struct CharacterProfession
    {
        /// <summary>职业 id（→ <see cref="ProfessionDefinition.id"/>）。</summary>
        public string professionRef;

        /// <summary>当前等级。</summary>
        public int level;

        /// <summary>当前等级内已积累经验。</summary>
        public int currentExp;

        /// <summary>主职业（支持多职业时区分；一个角色至多一个为 true）。</summary>
        public bool isPrimary;

        public CharacterProfession(string professionRef, int level = 1, int currentExp = 0, bool isPrimary = false)
        {
            this.professionRef = professionRef;
            this.level         = level;
            this.currentExp    = currentExp;
            this.isPrimary     = isPrimary;
        }
    }
}
