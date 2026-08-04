using System;
using System.Collections.Generic;

namespace Ale.Chronicle
{
    /// <summary>
    /// 转职树中的一个节点：一个职业 + 其可转职去向。以 <see cref="professionRef"/> 作树内节点标识。
    /// </summary>
    [Serializable]
    public class ProfessionTreeNode
    {
        /// <summary>本节点对应的职业 id（→ <see cref="ProfessionDefinition.id"/>；即树内节点标识）。</summary>
        public string professionRef;

        /// <summary>转职去向（子职业 professionRef，父→子表达「本职业可转职为…」）。</summary>
        public List<string> childProfessionRefs = new List<string>();

        public ProfessionTreeNode Clone() => new ProfessionTreeNode
        {
            professionRef       = professionRef,
            childProfessionRefs = childProfessionRefs != null ? new List<string>(childProfessionRefs) : new List<string>(),
        };
    }
}
