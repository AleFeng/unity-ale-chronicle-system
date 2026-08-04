using System;
using System.Collections.Generic;
using Ale.Toolkit.Runtime;

namespace Ale.Chronicle
{
    /// <summary>
    /// 一棵命名转职树（战士线 / 法师线…）：职业间的进阶 DAG。节点引用职业，父→子边表达「A 可转职为 B」。
    /// <para>根职业派生自「作为节点、但不是任何节点的子」，不单独存储；展开态是编辑器瞬态、不序列化。
    /// 默认单父推荐（多父仅警告），同一职业可自由存在于多棵树。</para>
    /// </summary>
    [Serializable]
    public class ProfessionTree
    {
        /// <summary>稳定引用键。</summary>
        public string id;

        /// <summary>显示名（Text）。</summary>
        public AttributeValue displayName = new AttributeValue(EFieldType.Text);

        /// <summary>树节点。</summary>
        public List<ProfessionTreeNode> nodes = new List<ProfessionTreeNode>();

        public ProfessionTree() { }

        public ProfessionTree(string id) { this.id = id; }

        /// <summary>根职业 = 在本树里作为节点、但不出现在任何节点的 childProfessionRefs 中。</summary>
        public IEnumerable<string> Roots()
        {
            var children = new HashSet<string>();
            foreach (var n in nodes)
            {
                if (n == null || n.childProfessionRefs == null) continue;
                foreach (var c in n.childProfessionRefs) children.Add(c);
            }
            foreach (var n in nodes)
                if (n != null && !string.IsNullOrEmpty(n.professionRef) && !children.Contains(n.professionRef))
                    yield return n.professionRef;
        }

        /// <summary>按职业 id 查节点（未命中返回 null）。</summary>
        public ProfessionTreeNode FindNode(string professionRef)
        {
            if (string.IsNullOrEmpty(professionRef) || nodes == null) return null;
            foreach (var n in nodes)
                if (n != null && n.professionRef == professionRef) return n;
            return null;
        }

        public void Normalize()
        {
            if (displayName == null) displayName = new AttributeValue(EFieldType.Text);
            else if (displayName.Type != EFieldType.Text || displayName.IsArray)
                displayName.ChangeType(EFieldType.Text, false);
            if (nodes == null) nodes = new List<ProfessionTreeNode>();
        }

        public ProfessionTree Clone()
        {
            var c = new ProfessionTree
            {
                id          = id,
                displayName = displayName != null ? displayName.Clone() : new AttributeValue(EFieldType.Text),
            };
            if (nodes != null)
                foreach (var n in nodes) c.nodes.Add(n != null ? n.Clone() : new ProfessionTreeNode());
            return c;
        }
    }
}
