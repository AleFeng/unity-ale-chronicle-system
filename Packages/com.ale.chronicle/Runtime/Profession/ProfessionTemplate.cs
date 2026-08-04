using System;
using Ale.Toolkit.Runtime;

namespace Ale.Chronicle
{
    /// <summary>
    /// 职业模板：一族职业（如「战斗职业」「生产职业」）的蓝本。承载 name / color / 自定义字段 schema
    /// （继承自 <see cref="ConfigTemplateBase"/> 的 <see cref="ConfigTemplateBase.attributes"/>）
    /// 以及默认预设——新建 <see cref="ProfessionDefinition"/> 实例时复制这些默认值，实例 <c>values</c> 依 schema 对账。
    /// 同时作为职业中列的分类筛选维。
    /// </summary>
    [Serializable]
    public class ProfessionTemplate : ConfigTemplateBase
    {
        /// <summary>默认等级上限（从模板创建职业时复制）。</summary>
        public int maxLevel = 10;

        public ProfessionTemplate() { }

        public ProfessionTemplate(string nameArg) : base(nameArg) { }

        /// <summary>深拷贝（名称 / 色点 / 字段 schema 由基类 <see cref="ConfigTemplateBase.CopyTo"/> 复制）。</summary>
        public ProfessionTemplate Clone()
        {
            var clone = new ProfessionTemplate
            {
                maxLevel = maxLevel,
            };
            CopyTo(clone);
            return clone;
        }
    }
}
