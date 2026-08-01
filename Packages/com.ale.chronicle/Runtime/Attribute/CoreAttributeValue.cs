using System;

namespace Ale.Chronicle
{
    /// <summary>
    /// 角色身上某核心属性的基础值（属性点分配 / 随机生成的结果）。
    /// 当前值不存储，由 <see cref="CoreAttributeResolver"/> 用「基础值 + 修饰器栈」现场汇总。
    /// </summary>
    [Serializable]
    public struct CoreAttributeValue
    {
        /// <summary>对应 <see cref="CoreAttributeDefinition.id"/>。</summary>
        public string attrId;

        /// <summary>该角色此属性的基础值。</summary>
        public float baseValue;

        public CoreAttributeValue(string attrId, float baseValue)
        {
            this.attrId    = attrId;
            this.baseValue = baseValue;
        }
    }
}
