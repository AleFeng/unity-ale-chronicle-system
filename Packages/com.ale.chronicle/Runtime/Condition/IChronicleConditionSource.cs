namespace Ale.Chronicle
{
    /// <summary>
    /// 角色条件判定的数据源：判定器经 <c>IConditionContext.GetService&lt;IChronicleConditionSource&gt;()</c> 取得，
    /// 用它按<see cref="EConditionScope"/>查询角色状态。
    /// <para>配置层只定义契约；实现由运行时（角色实例 / ECS 世界）在求值上下文里提供。</para>
    /// </summary>
    public interface IChronicleConditionSource
    {
        /// <summary>作用域角色是否拥有指定特质。</summary>
        bool HasTrait(EConditionScope scope, string traitId);

        /// <summary>作用域角色某核心属性的当前值（经修饰器汇流后）。</summary>
        float GetCoreAttribute(EConditionScope scope, string attrId);

        /// <summary>作用域角色的年龄（岁）。</summary>
        int GetAge(EConditionScope scope);
    }
}
