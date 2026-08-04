namespace Ale.Chronicle
{
    /// <summary>
    /// 经验成长曲线的表达模式（见 <see cref="ExpCurve"/>）。三选一：
    /// <see cref="Formula"/> 规整指数/线性；<see cref="Table"/> 精确控制每一级；<see cref="Curve"/> 手调曲线形状。
    /// <para>枚举值显式赋值且承诺稳定，防止旧数据损坏。</para>
    /// </summary>
    public enum EExpCurveMode
    {
        /// <summary>公式：<c>expToNext(level) = round(baseExp × level^exponent + linear × level)</c>。</summary>
        Formula = 0,

        /// <summary>表格：显式每级所需经验（索引 level-1 → 升到 level+1 所需；超表尾按末项）。</summary>
        Table = 1,

        /// <summary>曲线：<c>AnimationCurve(level → expToNext) × curveScale</c>。</summary>
        Curve = 2,
    }
}
