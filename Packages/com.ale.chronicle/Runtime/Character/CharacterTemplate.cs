using System;
using System.Collections.Generic;
using Ale.Toolkit.Runtime;

namespace Ale.Chronicle
{
    /// <summary>
    /// 角色模板：新建角色的蓝本。承载「身份自由字段 schema」（继承自 <see cref="ConfigTemplateBase"/> 的
    /// <see cref="ConfigTemplateBase.attributes"/>：姓名 / 生日 / 性别 / 身高…的字段定义），
    /// 以及一组<b>生成规则字段（预留）</b>——本阶段仅声明存储，不参与任何生成逻辑，随「角色生成 / 种族」子系统消费。
    /// </summary>
    [Serializable]
    public class CharacterTemplate : ConfigTemplateBase
    {
        // ── 生成规则（预留，本阶段不消费）────────────────────────────────────────────

        /// <summary>预留：默认种族引用（种族系统落地后消费）。</summary>
        public string raceRef;

        /// <summary>预留：出生必带特质 id 列表。</summary>
        public List<string> guaranteedTraitRefs = new List<string>();

        /// <summary>预留：随机特质候选池 id 列表（按各特质 birthChance 抽取）。</summary>
        public List<string> randomTraitPoolRefs = new List<string>();

        /// <summary>预留：属性点预算（随机 / 分配核心属性基础值时使用）。</summary>
        public int attributePointBudget;

        /// <summary>预留：初始年龄下限（世界日）。</summary>
        public int minAgeDays;

        /// <summary>预留：初始年龄上限（世界日）。</summary>
        public int maxAgeDays;

        public CharacterTemplate() { }

        public CharacterTemplate(string nameArg) : base(nameArg) { }

        /// <summary>深拷贝（名称 / 色点 / 字段 schema 由基类 <see cref="ConfigTemplateBase.CopyTo"/> 复制）。</summary>
        public CharacterTemplate Clone()
        {
            var clone = new CharacterTemplate
            {
                raceRef              = raceRef,
                attributePointBudget = attributePointBudget,
                minAgeDays           = minAgeDays,
                maxAgeDays           = maxAgeDays,
                guaranteedTraitRefs  = new List<string>(guaranteedTraitRefs),
                randomTraitPoolRefs  = new List<string>(randomTraitPoolRefs),
            };
            CopyTo(clone);
            return clone;
        }
    }
}
