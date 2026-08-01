using System;

namespace Ale.Chronicle
{
    /// <summary>数值比较方式（与 op 参数下拉选项顺序一致）+ 比较函数。供属性比较等判定器复用。</summary>
    public static class ChronicleCompareOp
    {
        public const int Greater        = 0; // 大于
        public const int GreaterOrEqual = 1; // 大于等于
        public const int Equal          = 2; // 等于
        public const int LessOrEqual    = 3; // 小于等于
        public const int Less           = 4; // 小于

        public static readonly string[] Labels = { "大于", "大于等于", "等于", "小于等于", "小于" };

        public static bool Compare(double a, int op, double b)
        {
            switch (op)
            {
                case Greater:        return a >  b;
                case GreaterOrEqual: return a >= b;
                case Equal:          return Math.Abs(a - b) < 1e-6;
                case LessOrEqual:    return a <= b;
                case Less:           return a <  b;
                default:             return a >= b;
            }
        }
    }
}
