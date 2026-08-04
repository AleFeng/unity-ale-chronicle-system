using System;
using System.Collections.Generic;
using Ale.Toolkit.Runtime;

namespace Ale.Chronicle
{
    /// <summary>
    /// 一条阶级头衔阶梯（男爵 → 子爵 → … → 公爵）：有序、逐级晋升、同时只持其一。
    /// <see cref="orderedTitleRefs"/> 低 → 高，元素均须是 <see cref="ETitleKind.RankTitle"/> 类头衔。
    /// 「同时只持其一」由运行时 <c>TitleRuntimeManager.Grant</c> 强制（晋升即替换同阶梯前者）。
    /// </summary>
    [Serializable]
    public class RankLadder
    {
        /// <summary>稳定引用键。</summary>
        public string id;

        /// <summary>显示名（Text）。</summary>
        public AttributeValue displayName = new AttributeValue(EFieldType.Text);

        /// <summary>阶梯成员头衔 id，低 → 高（→ <see cref="TitleDefinition.id"/>，均须 kind==RankTitle）。</summary>
        public List<string> orderedTitleRefs = new List<string>();

        public RankLadder() { }

        public RankLadder(string id) { this.id = id; }

        public void Normalize()
        {
            if (displayName == null) displayName = new AttributeValue(EFieldType.Text);
            else if (displayName.Type != EFieldType.Text || displayName.IsArray)
                displayName.ChangeType(EFieldType.Text, false);
            if (orderedTitleRefs == null) orderedTitleRefs = new List<string>();
        }

        public RankLadder Clone() => new RankLadder
        {
            id               = id,
            displayName      = displayName != null ? displayName.Clone() : new AttributeValue(EFieldType.Text),
            orderedTitleRefs = orderedTitleRefs != null ? new List<string>(orderedTitleRefs) : new List<string>(),
        };
    }
}
