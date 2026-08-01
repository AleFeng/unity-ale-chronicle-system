namespace Ale.Chronicle
{
    /// <summary>
    /// 修饰器目标类别（<b>预留</b>）。当前 toolkit 的 <c>ModifierDefinition</c> 默认作用于核心属性；
    /// 引入健康系统后，可据此把目标扩展为「身体能力 / 保留字段 / 派生属性」，令同一套修饰器机制
    /// 覆盖所有跨子系统数值影响。本阶段仅声明，尚未接入求值。
    /// </summary>
    public enum EModifierTargetKind
    {
        /// <summary>核心属性（默认）。</summary>
        CoreAttribute = 0,

        /// <summary>身体能力（移动 / 操作 / 意识 / 视觉 / 生育…，健康系统引入）。</summary>
        BodyCapacity = 1,

        /// <summary>保留字段（如 fertility）。</summary>
        WellKnownField = 2,

        /// <summary>派生属性（如 prowess）。</summary>
        DerivedAttribute = 3,
    }
}
