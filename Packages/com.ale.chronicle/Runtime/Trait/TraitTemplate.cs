using System;
using Ale.Toolkit.Runtime;

namespace Ale.Chronicle
{
    /// <summary>
    /// 特质模板：一族特质（如「性格」「疾病」）的蓝本。承载 name / color / 自定义字段 schema
    /// （继承自 <see cref="ConfigTemplateBase"/>）以及默认类别/时效——新建 <see cref="TraitDefinition"/> 实例时复制这些默认值，
    /// 实例 <c>values</c> 依 schema 对账。
    /// </summary>
    [Serializable]
    public class TraitTemplate : ConfigTemplateBase
    {
        /// <summary>默认特质类别（EnumType 名）。</summary>
        public string categoryEnumRef;

        /// <summary>默认时效。</summary>
        public ETraitLifetime lifetime = ETraitLifetime.Permanent;

        /// <summary>默认临时存活天数。</summary>
        public float defaultDurationDays;

        public TraitTemplate() { }

        public TraitTemplate(string nameArg) : base(nameArg) { }

        /// <summary>深拷贝（名称 / 色点 / 字段 schema 由基类 <see cref="ConfigTemplateBase.CopyTo"/> 复制）。</summary>
        public TraitTemplate Clone()
        {
            var clone = new TraitTemplate
            {
                categoryEnumRef     = categoryEnumRef,
                lifetime            = lifetime,
                defaultDurationDays = defaultDurationDays,
            };
            CopyTo(clone);
            return clone;
        }
    }
}
