using System.Collections.Generic;
using Ale.Toolkit.Runtime;
using Ale.Condition;

namespace Ale.Chronicle
{
    /// <summary>
    /// 核心属性汇流求值器：把「基础值 + 一组作用于该属性的 <see cref="ModifierDefinition"/>」交给 toolkit 的
    /// <see cref="ModifierStackEvaluator"/> 求出「当前值 + 逐来源明细」，并按属性定义的 <c>min/max</c> 夹取。
    ///
    /// <para>返回 toolkit 的 <see cref="ModifierEvaluation"/>（<c>BaseValue / RawValue / Value / Breakdown</c>），
    /// 供编辑器「基础 10 →(17) ⓘ 种族2/职业3/特质2」明细展示，运行时同一路径求值。</para>
    ///
    /// <para><b>本阶段</b>提供低层求值（调用方给出作用于同一属性的修饰器集合）；
    /// 「从角色各来源（特质 / 种族 / 职业 / …）按 targetAttributeId 收集并分组」的高层重载，
    /// 随顶层角色系统的组合装配落地。</para>
    /// </summary>
    public static class CoreAttributeResolver
    {
        /// <summary>用显式基础值求值（clamp 到 <paramref name="def"/> 的 [min, max]）。</summary>
        public static ModifierEvaluation Evaluate(CoreAttributeDefinition def, float baseValue,
            IEnumerable<ModifierDefinition> modifiers)
        {
            float min = def != null ? def.minValue : float.NegativeInfinity;
            float max = def != null ? def.maxValue : float.PositiveInfinity;
            return ModifierStackEvaluator.Evaluate(baseValue, min, max, modifiers);
        }

        /// <summary>用角色的核心属性基础值求值。</summary>
        public static ModifierEvaluation Evaluate(CoreAttributeDefinition def, CoreAttributeValue value,
            IEnumerable<ModifierDefinition> modifiers)
            => Evaluate(def, value.baseValue, modifiers);

        /// <summary>用属性定义的默认基础值求值。</summary>
        public static ModifierEvaluation Evaluate(CoreAttributeDefinition def,
            IEnumerable<ModifierDefinition> modifiers)
            => Evaluate(def, def != null ? def.defaultBase : 0f, modifiers);

        /// <summary>
        /// <b>高层重载</b>：直接对一个角色的某核心属性汇流求值——从 <paramref name="character"/> 的核心属性基础值起，
        /// 收集其作用于 <paramref name="def"/> 的全部修饰器（特质 / 职业每级成长 / 头衔加成，经 <paramref name="src"/>
        /// 反查定义、打好来源标记），交由低层求值并按 <paramref name="def"/> 的 min/max 夹取。
        /// 编辑器「基础 → 当前 + 来源明细」预览与运行时共用此入口。
        /// </summary>
        public static ModifierEvaluation Evaluate(CharacterDefinition character, CoreAttributeDefinition def,
            IChronicleSchemaSource src)
            => Evaluate(character, def, src, null);

        /// <summary>
        /// 同上，另接收条件上下文 <paramref name="ctx"/>：在收集期把 <paramref name="def"/> 自身携带、且条件通过的
        /// <see cref="CoreAttributeDefinition.conditionalModifiers"/> 也汇入（来源标记 <c>attr:{id}:cond</c>）。
        /// 空条件恒通过；非空条件在 <paramref name="ctx"/> 为 null（如编辑器预览、无真实条件源）时判定器取不到数据源 →
        /// 不通过 → 该条修改值不计入（运行时传入真实上下文才生效）。既有三来源汇流不受影响。
        /// </summary>
        public static ModifierEvaluation Evaluate(CharacterDefinition character, CoreAttributeDefinition def,
            IChronicleSchemaSource src, IConditionContext ctx)
        {
            string attrId = def != null ? def.id : null;
            var mods = new List<ModifierDefinition>();
            character?.CollectModifiers(attrId, src, mods);
            CollectConditionalModifiers(def, ctx, mods);
            float baseValue = character != null
                ? character.GetCoreBaseValue(attrId, def)
                : (def != null ? def.defaultBase : 0f);
            return Evaluate(def, baseValue, mods);
        }

        /// <summary>
        /// 把 <paramref name="def"/> 自身携带、且条件通过的条件修改值克隆汇入 <paramref name="into"/>
        /// （靶子回填为本属性 id；无自带来源标记则打 <c>attr:{id}:cond</c>）。空条件恒通过；
        /// 条件不通过（含 <paramref name="ctx"/> 为 null 时的非空条件）者不计入。
        /// </summary>
        public static void CollectConditionalModifiers(CoreAttributeDefinition def, IConditionContext ctx,
            List<ModifierDefinition> into)
        {
            if (def == null || def.conditionalModifiers == null || into == null) return;
            string attrId = def.id;
            string srcTag = "attr:" + attrId + ":cond";
            foreach (var cm in def.conditionalModifiers)
            {
                if (cm == null || cm.modifier == null) continue;
                bool pass = cm.condition == null || cm.condition.IsEmpty || cm.condition.Evaluate(ctx).Passed;
                if (!pass) continue;
                var m = cm.modifier.Clone();
                m.targetAttributeId = attrId;
                if (string.IsNullOrEmpty(m.sourceTag)) m.sourceTag = srcTag;
                into.Add(m);
            }
        }
    }
}
