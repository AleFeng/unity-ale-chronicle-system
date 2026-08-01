using System;
using Ale.Toolkit.Runtime;

namespace Ale.Chronicle
{
    /// <summary>
    /// 编年史分组标签（与 toolkit <see cref="GroupTag"/> 同形：id / 显示名 / 描述 / 色点，无自定义字段）。
    /// 通用的具名分组载体，供后续子系统按 1 主分组 + 若干副分组的形式引用。
    /// <para><b>本阶段暂无运行时消费者</b>——作为基础设施先行，随社交 / 头衔 / 技能等子系统接入。</para>
    /// </summary>
    [Serializable]
    public class ChronicleGroupTag : GroupTag
    {
        public ChronicleGroupTag() { }

        public ChronicleGroupTag(string id, string displayName = null) : base(id, displayName) { }

        /// <summary>深拷贝。</summary>
        public ChronicleGroupTag Clone()
        {
            var clone = new ChronicleGroupTag();
            CopyTo(clone);
            return clone;
        }
    }
}
