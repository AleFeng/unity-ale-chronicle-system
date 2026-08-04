using System;
using System.Collections.Generic;
using Ale.Toolkit.Runtime;
using UnityEngine;

namespace Ale.Chronicle
{
    /// <summary>
    /// 职业升级所需经验曲线。三模式（<see cref="EExpCurveMode"/>）共用统一求值入口 <see cref="ExpToNext"/> /
    /// <see cref="TotalExpForLevel"/>，编辑器预览与运行时升级循环都据此计算。
    /// <para>曲线用 <see cref="AttributeValue"/>(<see cref="EFieldType.AnimationCurve"/>) 承载——原生序列化、
    /// 原生绘制器，无需自写曲线编解码。</para>
    /// </summary>
    [Serializable]
    public class ExpCurve
    {
        /// <summary>曲线表达模式。</summary>
        public EExpCurveMode mode = EExpCurveMode.Formula;

        // —— Formula ——
        public float baseExp  = 100f;
        public float exponent = 1.5f;
        public float linear   = 0f;

        // —— Table：索引 (level-1) → 升到 level+1 所需经验 ——
        public List<int> perLevelExp = new List<int>();

        // —— Curve：AnimationCurve(level → expToNext) × curveScale ——
        public AttributeValue curveValue = new AttributeValue(EFieldType.AnimationCurve);
        public float curveScale = 1f;

        /// <summary>从 <paramref name="level"/> 升到 level+1 所需经验（统一求值入口）。</summary>
        public int ExpToNext(int level)
        {
            switch (mode)
            {
                case EExpCurveMode.Table:
                    if (perLevelExp == null || perLevelExp.Count == 0) return 0;
                    int i = Mathf.Clamp(level - 1, 0, perLevelExp.Count - 1);
                    return perLevelExp[i];
                case EExpCurveMode.Curve:
                    var c = curveValue != null ? curveValue.GetAnimationCurve(0) : null;
                    return c == null ? 0 : Mathf.RoundToInt(c.Evaluate(level) * curveScale);
                default: // Formula
                    return Mathf.RoundToInt(baseExp * Mathf.Pow(level, exponent) + linear * level);
            }
        }

        /// <summary>累计到 <paramref name="level"/> 所需总经验（level 1 → 0）。</summary>
        public int TotalExpForLevel(int level)
        {
            int sum = 0;
            for (int k = 1; k < level; k++) sum += ExpToNext(k);
            return sum;
        }

        /// <summary>归一：确保曲线字段类型正确、列表非空。</summary>
        public void Normalize()
        {
            if (curveValue == null) curveValue = new AttributeValue(EFieldType.AnimationCurve);
            else if (curveValue.Type != EFieldType.AnimationCurve || curveValue.IsArray)
                curveValue.ChangeType(EFieldType.AnimationCurve, false);
            if (perLevelExp == null) perLevelExp = new List<int>();
        }

        public ExpCurve Clone() => new ExpCurve
        {
            mode        = mode,
            baseExp     = baseExp,
            exponent    = exponent,
            linear      = linear,
            perLevelExp = perLevelExp != null ? new List<int>(perLevelExp) : new List<int>(),
            curveValue  = curveValue != null ? curveValue.Clone() : new AttributeValue(EFieldType.AnimationCurve),
            curveScale  = curveScale,
        };
    }
}
