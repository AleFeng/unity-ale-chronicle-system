using System;
using Ale.Toolkit.Runtime;

namespace Ale.Chronicle
{
    /// <summary>
    /// 头衔模板：一族头衔（如「世俗爵位」「教会头衔」「军功称号」）的蓝本。承载 name / color / 自定义字段 schema
    /// （继承自 <see cref="ConfigTemplateBase"/> 的 <see cref="ConfigTemplateBase.attributes"/>）
    /// 以及默认预设——新建 <see cref="TitleDefinition"/> 实例时复制这些默认值，实例 <c>values</c> 依 schema 对账。
    /// 同时作为头衔中列的分类筛选维。
    /// </summary>
    [Serializable]
    public class TitleTemplate : ConfigTemplateBase
    {
        /// <summary>默认头衔种类（阶级头衔 / 称号）。</summary>
        public ETitleKind kind = ETitleKind.RankTitle;

        /// <summary>默认是否可被剥夺。</summary>
        public bool isRevocable = true;

        public TitleTemplate() { }

        public TitleTemplate(string nameArg) : base(nameArg) { }

        /// <summary>深拷贝（名称 / 色点 / 字段 schema 由基类 <see cref="ConfigTemplateBase.CopyTo"/> 复制）。</summary>
        public TitleTemplate Clone()
        {
            var clone = new TitleTemplate
            {
                kind        = kind,
                isRevocable = isRevocable,
            };
            CopyTo(clone);
            return clone;
        }
    }
}
