namespace Ale.Chronicle
{
    /// <summary>
    /// 条件 / 效果的作用域：判定器 / 效果指向哪个角色。运行时由数据源按主体 / 对象等关系解析为具体角色。
    /// 值连续从 0 起，与 <see cref="ChronicleConditionScopes.Labels"/> 一一对应（下拉存索引即 = 枚举值）。
    /// </summary>
    public enum EConditionScope
    {
        /// <summary>主体（发起者 / 被判定者）。</summary>
        Actor = 0,

        /// <summary>对象（交互接收方）。</summary>
        Recipient = 1,

        /// <summary>第三方。</summary>
        Secondary = 2,

        /// <summary>主体的配偶。</summary>
        ActorSpouse = 3,

        /// <summary>主体的父亲。</summary>
        ActorFather = 4,

        /// <summary>主体的母亲。</summary>
        ActorMother = 5,
    }

    /// <summary>作用域的下拉标签（顺序 = <see cref="EConditionScope"/> 值）。</summary>
    public static class ChronicleConditionScopes
    {
        public static readonly string[] Labels =
        {
            "主体", "对象", "第三方", "主体配偶", "主体父亲", "主体母亲",
        };
    }
}
