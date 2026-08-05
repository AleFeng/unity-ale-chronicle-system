using System;
using Ale.Toolkit.Runtime;
using Ale.Condition;

namespace Ale.Chronicle
{
    /// <summary>
    /// 条件化修改器（payload + gate 范式，仿 toolkit <c>EffectItem</c>）：一条 <see cref="ModifierDefinition"/> 修改值
    /// 加一个可选条件门控 <see cref="condition"/>。门控为空 = 无条件生效；非空则运行时条件满足才汇入。
    /// 由 <see cref="CoreAttributeDefinition"/> 承载，经 <see cref="CoreAttributeResolver"/> 在汇流收集期按条件过滤。
    /// </summary>
    [Serializable]
    public class ConditionalModifier
    {
        /// <summary>要施加的修饰器（toolkit 类型；靶子 <c>targetAttributeId</c> 由属性定义自身填充）。</summary>
        public ModifierDefinition modifier = new ModifierDefinition();

        /// <summary>门控条件（Condition System；空 = 无条件生效）。</summary>
        public ConditionExpression condition = new ConditionExpression();

        public void Normalize()
        {
            if (modifier == null)  modifier  = new ModifierDefinition();
            if (condition == null) condition = new ConditionExpression();
        }

        public ConditionalModifier Clone() => new ConditionalModifier
        {
            modifier  = modifier != null ? modifier.Clone() : new ModifierDefinition(),
            condition = condition != null ? condition.Clone() : new ConditionExpression(),
        };
    }
}
