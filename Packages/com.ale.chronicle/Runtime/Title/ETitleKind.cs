namespace Ale.Chronicle
{
    /// <summary>
    /// 头衔种类。<see cref="RankTitle"/>（阶级头衔）由 <see cref="RankLadder"/> 组织成有序阶梯——
    /// 逐级晋升、同时只持其一；<see cref="Epithet"/>（称号）靠事迹获得、多为唯一、不入阶梯。
    /// <para>枚举值显式赋值且承诺稳定，防止旧数据损坏。</para>
    /// </summary>
    public enum ETitleKind
    {
        /// <summary>阶级头衔（爵位 / 军衔类）：有序阶梯、逐级晋升、同时只持其一。</summary>
        RankTitle = 0,

        /// <summary>称号（剑圣 / 征服王）：靠事迹获得、多为唯一、不入阶梯。</summary>
        Epithet = 1,
    }
}
