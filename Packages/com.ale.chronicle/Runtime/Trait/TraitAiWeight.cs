using System;

namespace Ale.Chronicle
{
    /// <summary>
    /// 特质对某条 AI 行为轴的权重偏移（数据驱动，轴由 EnumType 定义，如 勇武 / 贪婪 / 慈悲 / 理性…）。
    /// 运行时 AI 决策据此加权；本阶段仅配置，不参与求值。
    /// </summary>
    [Serializable]
    public struct TraitAiWeight
    {
        /// <summary>AI 行为轴（EnumItem 名，稳定键，裸 string）。</summary>
        public string axisRef;

        /// <summary>该轴上的权重偏移（正 = 倾向，负 = 回避）。</summary>
        public float weight;

        public TraitAiWeight(string axisRef, float weight)
        {
            this.axisRef = axisRef;
            this.weight  = weight;
        }
    }
}
