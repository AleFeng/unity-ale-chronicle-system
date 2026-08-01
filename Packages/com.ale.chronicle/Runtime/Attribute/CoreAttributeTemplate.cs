using System;
using Ale.Toolkit.Runtime;

namespace Ale.Chronicle
{
    /// <summary>
    /// 核心属性模板：一族核心属性（如「体格类」「心智类」）的蓝本。承载 name / color / 自定义字段 schema
    /// （继承自 <see cref="ConfigTemplateBase"/> 的 <see cref="ConfigTemplateBase.attributes"/>）
    /// 以及默认区间/类别——新建 <see cref="CoreAttributeDefinition"/> 实例时复制这些默认值，实例 <c>values</c> 依 schema 对账。
    /// </summary>
    [Serializable]
    public class CoreAttributeTemplate : ConfigTemplateBase
    {
        /// <summary>默认属性类别（EnumType 名）。</summary>
        public string categoryEnumRef;

        /// <summary>默认下限。</summary>
        public float minValue = 0f;

        /// <summary>默认上限。</summary>
        public float maxValue = 100f;

        /// <summary>默认基础值。</summary>
        public float defaultBase = 0f;

        public CoreAttributeTemplate() { }

        public CoreAttributeTemplate(string nameArg) : base(nameArg) { }

        /// <summary>深拷贝（名称 / 色点 / 字段 schema 由基类 <see cref="ConfigTemplateBase.CopyTo"/> 复制）。</summary>
        public CoreAttributeTemplate Clone()
        {
            var clone = new CoreAttributeTemplate
            {
                categoryEnumRef = categoryEnumRef,
                minValue        = minValue,
                maxValue        = maxValue,
                defaultBase     = defaultBase,
            };
            CopyTo(clone);
            return clone;
        }
    }
}
