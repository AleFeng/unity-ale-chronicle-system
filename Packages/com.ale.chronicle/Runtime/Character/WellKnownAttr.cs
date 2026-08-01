namespace Ale.Chronicle
{
    /// <summary>
    /// 「众所周知」的角色自由字段稳定 id 常量：这些字段以 <c>AttributeEntry</c> 存于
    /// <see cref="CharacterDefinition.values"/>（由模板 schema 定义），但系统在多处按固定 id 读取
    /// （如出生日算年龄、性别决定繁殖角色…）。集中成常量，避免各处硬编码魔法字符串漂移。
    /// </summary>
    public static class WellKnownAttr
    {
        /// <summary>姓名（String/Text）。</summary>
        public const string Name = "name";

        /// <summary>出生世界日（Int）：角色年龄 = 当前世界日 − 该值。</summary>
        public const string Birthday = "birthday";

        /// <summary>性别（Enum，枚举类型由配置定义）。</summary>
        public const string Sex = "sex";

        /// <summary>身高（Float）。</summary>
        public const string Height = "height";

        /// <summary>体重（Float）。</summary>
        public const string Weight = "weight";

        /// <summary>健康度（Float；健康系统引入后可改由能力层汇总）。</summary>
        public const string Health = "health";

        /// <summary>生育力（Float）。</summary>
        public const string Fertility = "fertility";
    }
}
