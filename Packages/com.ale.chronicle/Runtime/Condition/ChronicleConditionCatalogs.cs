namespace Ale.Chronicle
{
    /// <summary>
    /// 编年史判定器参数的<b>候选目录引用</b>常量：判定器 schema 用 <c>catalogRef</c> 声明「这个字符串参数装的是哪一类 id」，
    /// 编辑器侧的 <c>ChronicleConditionParamProvider</c> 按同一常量供给候选，把裸文本框换成分组下拉。
    /// <para>放在运行时程序集是因为判定器 schema（运行时代码）要引用它；编辑器侧只是复用同一批字符串。</para>
    /// </summary>
    public static class ChronicleConditionCatalogs
    {
        /// <summary>核心属性 id。</summary>
        public const string Attribute = "Chronicle.Attribute";

        /// <summary>特质 id。</summary>
        public const string Trait = "Chronicle.Trait";

        /// <summary>头衔 id。</summary>
        public const string Title = "Chronicle.Title";

        /// <summary>职业 id。</summary>
        public const string Profession = "Chronicle.Profession";

        /// <summary>阶级序列 id。</summary>
        public const string RankLadder = "Chronicle.RankLadder";
    }
}
