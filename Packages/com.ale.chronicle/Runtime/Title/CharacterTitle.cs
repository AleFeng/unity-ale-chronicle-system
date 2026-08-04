using System;

namespace Ale.Chronicle
{
    /// <summary>
    /// 角色实例上持有的一个头衔（编辑期起始配装）。运行时持有由 <c>TitleRuntimeManager</c> 持有并存档；
    /// 本结构用于配置与编辑器汇流预览。
    /// </summary>
    [Serializable]
    public struct CharacterTitle
    {
        /// <summary>头衔 id（→ <see cref="TitleDefinition.id"/>）。</summary>
        public string titleRef;

        /// <summary>获得日期（世界内天数；用于排序 / 历史）。</summary>
        public int acquiredWorldDay;

        public CharacterTitle(string titleRef, int acquiredWorldDay = 0)
        {
            this.titleRef         = titleRef;
            this.acquiredWorldDay = acquiredWorldDay;
        }
    }
}
