#if ATK_TMP
using UiText = TMPro.TMP_Text;
#else
using UiText = UnityEngine.UI.Text;
#endif

using System.Collections.Generic;
using System.Text;
using Ale.Toolkit.Runtime;
using Ale.Toolkit.Runtime.UI;
using UnityEngine;

namespace Ale.Chronicle.Runtime.UI
{
    /// <summary>
    /// 角色信息主界面（MonoBehaviour，继承 <see cref="UiwViewBase"/>，与 <see cref="UiwSkillView"/> 同规格）。
    /// 从 <see cref="ChronicleDataManager"/> 取 <see cref="CharacterDefinition"/>，分区展示：
    /// 个人档案（姓名/性别/年龄/身高/体重/三围/血型/兴趣）、6 项能力（基础 → 当前 + 逐来源明细，经
    /// <see cref="CoreAttributeResolver"/> 汇流特质/职业成长/头衔）、特质、职业（等级/主职业）、头衔（含爵位阶级位次）、
    /// 以及由职业技能树导出的可用技能。
    ///
    /// <para>展示的是<b>静态配置</b>（角色的起始配装），无运行时进度订阅，故 <see cref="Unsubscribe"/> 为空、
    /// <see cref="Reopen"/> 直接重开。各分区文本写入对应的序列化 <c>UiText</c>（ATK_TMP 下即 TMP_Text）。</para>
    /// </summary>
    public class UiwCharacterView : UiwViewBase
    {
        [Header("角色")]
        [Tooltip("要展示的角色 ID（→ ChronicleDataManager.GetCharacter）。")]
        public string characterId = "luna";
        [Tooltip("用于计算年龄的当前世界日：年龄 = 当前世界日 − 出生世界日。")]
        public int worldDay;
        [Tooltip("把「世界日」换算为「岁」的每岁天数（仅用于显示；历法子系统落地前的近似）。")]
        public int daysPerYear = 365;

        [Header("展示文本（各分区，可按需留空）")]
        [Tooltip("个人档案：性别/年龄/身高/体重/三围/血型/兴趣。")]
        [SerializeField] private UiText profileLabel;
        [Tooltip("能力：基础 → 当前 + 逐来源明细。")]
        [SerializeField] private UiText attributesLabel;
        [Tooltip("特质（含携带的属性修饰器摘要）。")]
        [SerializeField] private UiText traitsLabel;
        [Tooltip("职业：显示名 + 等级（+ 主职业标记）。")]
        [SerializeField] private UiText professionsLabel;
        [Tooltip("头衔：称号；阶级头衔附带其在阶级序列中的位次。")]
        [SerializeField] private UiText titlesLabel;
        [Tooltip("技能：由角色各职业关联的技能树导出。")]
        [SerializeField] private UiText skillsLabel;

        #region 打开 / 关闭

        /// <summary>用当前 <see cref="characterId"/> 打开并构建各分区。</summary>
        public override void Open()
        {
            base.Open();   // 激活面板（公共步骤）
            Rebuild();
        }

        /// <summary>切换角色并（若已打开）刷新。</summary>
        public void Bind(string id)
        {
            characterId = id;
            if (IsOpen) Rebuild();
        }

        /// <summary>展示静态配置，无运行时事件订阅。</summary>
        protected override void Unsubscribe() { }

        /// <summary>用当前角色重新打开（供基类 <see cref="UiwViewBase.ToggleOpenClose"/>）。</summary>
        protected override void Reopen() => Open();

        #endregion

        #region 构建

        /// <summary>从数据管理器取角色并重建全部分区文本。</summary>
        private void Rebuild()
        {
            var dm = ChronicleDataManager.Instance;
            var character = dm != null ? dm.GetCharacter(characterId) : null;
            if (character == null)
            {
                if (titleLabel) titleLabel.text = $"(未找到角色：{characterId})";
                SetSections(string.Empty);
                return;
            }

            var src = FindSchemaSource(dm, characterId);

            if (titleLabel) titleLabel.text = ResolveName(character);
            // 分区标题烘焙进内容文本(视图每次覆盖写入,不依赖独立静态节点——后者会被 LocalizedTextEvent 清空)。
            if (profileLabel)     profileLabel.text     = Section("个人档案",        BuildProfile(character, dm));
            if (attributesLabel)  attributesLabel.text  = Section("能力（基础 → 当前）", BuildAttributes(character, dm, src));
            if (traitsLabel)      traitsLabel.text      = Section("特质",            BuildTraits(character, dm));
            if (professionsLabel) professionsLabel.text = Section("职业",            BuildProfessions(character, dm));
            if (titlesLabel)      titlesLabel.text      = Section("头衔",            BuildTitles(character, dm));
            if (skillsLabel)      skillsLabel.text      = Section("技能",            BuildSkills(character, dm));
        }

        /// <summary>把分区标题(TMP 富文本加粗)拼到内容之前;内容为空时占位「—」。</summary>
        private static string Section(string header, string body)
            => "<b>◆ " + header + "</b>\n" + (string.IsNullOrEmpty(body) ? "—" : body);

        private void SetSections(string text)
        {
            if (profileLabel)     profileLabel.text     = text;
            if (attributesLabel)  attributesLabel.text  = text;
            if (traitsLabel)      traitsLabel.text      = text;
            if (professionsLabel) professionsLabel.text = text;
            if (titlesLabel)      titlesLabel.text      = text;
            if (skillsLabel)      skillsLabel.text      = text;
        }

        /// <summary>个人档案：逐字段显示;枚举字段经 <see cref="ChronicleDataManager"/> 直解为枚举项名(与 SkillRankUtil 同范式,不依赖全局 EnumTypeResolver)。</summary>
        private string BuildProfile(CharacterDefinition c, ChronicleDataManager dm)
        {
            var sb = new StringBuilder();
            AppendField(sb, "性别", c, dm, WellKnownAttr.Sex);
            int ageDays = c.GetAge(worldDay);
            int years = daysPerYear > 0 ? ageDays / daysPerYear : ageDays;
            sb.Append("年龄：").Append(years).Append(" 岁（").Append(ageDays).Append(" 日）\n");
            AppendField(sb, "身高", c, dm, WellKnownAttr.Height, "cm");
            AppendField(sb, "体重", c, dm, WellKnownAttr.Weight, "kg");
            AppendField(sb, "三围", c, dm, "measurements");
            AppendField(sb, "血型", c, dm, "bloodType");
            AppendField(sb, "兴趣", c, dm, "interests");
            return sb.ToString().TrimEnd('\n');
        }

        private static void AppendField(StringBuilder sb, string label, CharacterDefinition c, ChronicleDataManager dm, string attrId, string suffix = null)
        {
            var av = c.GetAttributeValue(attrId);
            if (av == null) return;
            string val;
            if (av.Type == EFieldType.Enum)
            {
                // 运行时枚举值→枚举项名:经 ChronicleDataManager 直解(全局 EnumTypeResolver 运行时不保证已装)。
                var et   = dm != null ? dm.GetEnumType(av.EnumTypeRef) : null;
                var item = et != null ? et.GetItemByValue(av.AsEnumValue) : null;
                val = item != null ? item.name : av.AsInt.ToString();
            }
            else
            {
                val = av.ToDisplayString();
            }
            if (string.IsNullOrEmpty(val)) return;
            sb.Append(label).Append('：').Append(val);
            if (!string.IsNullOrEmpty(suffix)) sb.Append(' ').Append(suffix);
            sb.Append('\n');
        }

        /// <summary>能力：对角色配置了基础值的每个核心属性汇流求值，展示「基础 → 当前」与逐来源明细。</summary>
        private string BuildAttributes(CharacterDefinition c, ChronicleDataManager dm, IChronicleSchemaSource src)
        {
            var sb = new StringBuilder();
            foreach (var cv in c.coreAttributes)
            {
                var def = dm.GetCoreAttribute(cv.attrId);
                if (def == null) continue;
                var ev = CoreAttributeResolver.Evaluate(c, def, src);
                sb.Append(def.ResolveDisplayName()).Append('：')
                  .Append(Num(ev.BaseValue)).Append(" → ").Append(Num(ev.Value));

                // 汇流值超出 [min,max] 被夹取时标注原值,使右值与下方明细(其 Δ 之和 = 未夹取值−基础)自洽。
                if (!Mathf.Approximately(ev.RawValue, ev.Value))
                    sb.Append("（封顶，原值 ").Append(Num(ev.RawValue)).Append("）");

                if (ev.Breakdown != null && ev.Breakdown.Count > 0)
                {
                    sb.Append("  （");
                    for (int i = 0; i < ev.Breakdown.Count; i++)
                    {
                        var b = ev.Breakdown[i];
                        if (i > 0) sb.Append('，');
                        sb.Append(PrettySource(b.SourceTag, dm)).Append(Signed(b.Delta));
                    }
                    sb.Append('）');
                }
                sb.Append('\n');
            }
            return sb.ToString().TrimEnd('\n');
        }

        /// <summary>特质：显示名 + 携带的属性修饰器摘要。</summary>
        private string BuildTraits(CharacterDefinition c, ChronicleDataManager dm)
        {
            var sb = new StringBuilder();
            foreach (var ti in c.traits)
            {
                var t = dm.GetTrait(ti.traitRef);
                if (t == null) continue;
                sb.Append("· ").Append(t.ResolveDisplayName());
                if (t.modifiers != null && t.modifiers.Count > 0)
                {
                    // 先拼修饰器摘要(跳过 null、以 first 控分隔符),仅在非空时套括号——避免前导「，」或空「（）」。
                    var mods = new StringBuilder();
                    bool first = true;
                    foreach (var m in t.modifiers)
                    {
                        if (m == null) continue;
                        if (!first) mods.Append('，');
                        mods.Append(AttrName(dm, m.targetAttributeId)).Append(Signed(m.magnitude));
                        first = false;
                    }
                    if (mods.Length > 0) sb.Append('（').Append(mods).Append('）');
                }
                sb.Append('\n');
            }
            return sb.ToString().TrimEnd('\n');
        }

        /// <summary>职业：显示名 + 等级（+ 主职业标记）。</summary>
        private string BuildProfessions(CharacterDefinition c, ChronicleDataManager dm)
        {
            var sb = new StringBuilder();
            foreach (var cp in c.professions)
            {
                var p = dm.GetProfession(cp.professionRef);
                if (p == null) continue;
                sb.Append("· ").Append(p.ResolveDisplayName())
                  .Append("  Lv.").Append(cp.level).Append('/').Append(p.maxLevel);
                if (cp.isPrimary) sb.Append("  [主职业]");
                sb.Append('\n');
            }
            return sb.ToString().TrimEnd('\n');
        }

        /// <summary>头衔：称号直接显示；阶级头衔附带其在阶级序列中的位次（如「伯爵（爵位 3/5）」）。</summary>
        private string BuildTitles(CharacterDefinition c, ChronicleDataManager dm)
        {
            var sb = new StringBuilder();
            foreach (var ct in c.titles)
            {
                var t = dm.GetTitle(ct.titleRef);
                if (t == null) continue;
                sb.Append("· ").Append(t.ResolveDisplayName());
                if (t.kind == ETitleKind.RankTitle)
                {
                    var (ladder, pos, total) = FindRankPosition(dm, t.id);
                    if (ladder != null)
                        sb.Append('（').Append(ResolveLadderName(ladder)).Append(' ')
                          .Append(pos).Append('/').Append(total).Append('）');
                }
                sb.Append('\n');
            }
            return sb.ToString().TrimEnd('\n');
        }

        /// <summary>技能：遍历角色各职业关联的技能树，列出其中技能（按技能树分组、去重）。</summary>
        private string BuildSkills(CharacterDefinition c, ChronicleDataManager dm)
        {
            var sb = new StringBuilder();
            var seenTrees = new HashSet<string>();
            foreach (var cp in c.professions)
            {
                var p = dm.GetProfession(cp.professionRef);
                if (p == null || p.skillTreeRefs == null) continue;
                foreach (var treeId in p.skillTreeRefs)
                {
                    if (string.IsNullOrEmpty(treeId) || !seenTrees.Add(treeId)) continue;
                    var tree = dm.GetSkillTree(treeId);
                    if (tree == null || tree.skills == null) continue;

                    sb.Append("「").Append(ResolveTreeName(tree)).Append("」");
                    bool first = true;
                    foreach (var e in tree.skills)
                    {
                        if (e == null || string.IsNullOrEmpty(e.skillRef)) continue;
                        var skill = dm.GetSkill(e.skillRef);
                        if (skill == null) continue;
                        sb.Append(first ? "" : "、").Append(UiwSkillText.ResolveName(skill));
                        first = false;
                    }
                    sb.Append('\n');
                }
            }
            return sb.ToString().TrimEnd('\n');
        }

        #endregion

        #region 辅助

        /// <summary>找到包含该角色的已注册数据库作为求值 schema 源；找不到回退首个已注册库。</summary>
        private static IChronicleSchemaSource FindSchemaSource(ChronicleDataManager dm, string charId)
        {
            if (dm == null) return null;
            var dbs = dm.Databases;
            for (int i = 0; i < dbs.Count; i++)
                if (dbs[i] != null && dbs[i].GetCharacter(charId) != null) return dbs[i];
            return dbs.Count > 0 ? dbs[0] : null;
        }

        /// <summary>角色显示名：读 <see cref="WellKnownAttr.Name"/> 自由字段，空则回退角色 id。</summary>
        private static string ResolveName(CharacterDefinition c)
        {
            string n = c.GetAttributeValue<string>(WellKnownAttr.Name);
            return string.IsNullOrEmpty(n) ? c.id : n;
        }

        /// <summary>把修饰器来源标记翻译为可读来源名：trait:{id}→特质名、prof:{id}:growth→职业名+成长、title:{id}→头衔名。</summary>
        private static string PrettySource(string sourceTag, ChronicleDataManager dm)
        {
            if (string.IsNullOrEmpty(sourceTag)) return "?";

            const string trait = "trait:", title = "title:", prof = "prof:", growth = ":growth";
            if (sourceTag.StartsWith(trait))
            {
                var t = dm.GetTrait(sourceTag.Substring(trait.Length));
                return t != null ? t.ResolveDisplayName() : sourceTag;
            }
            if (sourceTag.StartsWith(title))
            {
                var t = dm.GetTitle(sourceTag.Substring(title.Length));
                return t != null ? t.ResolveDisplayName() : sourceTag;
            }
            if (sourceTag.StartsWith(prof))
            {
                string body = sourceTag.Substring(prof.Length);
                if (body.EndsWith(growth)) body = body.Substring(0, body.Length - growth.Length);
                var p = dm.GetProfession(body);
                return p != null ? p.ResolveDisplayName() + "成长" : sourceTag;
            }
            return sourceTag;
        }

        /// <summary>定位阶级头衔所属的阶级序列及其位次（1 基，低→高）。未找到返回 (null,0,0)。</summary>
        private static (RankLadder ladder, int pos, int total) FindRankPosition(ChronicleDataManager dm, string titleId)
        {
            foreach (var l in dm.GetAllRankLadders())
            {
                if (l == null || l.orderedTitleRefs == null) continue;
                int idx = l.orderedTitleRefs.IndexOf(titleId);
                if (idx >= 0) return (l, idx + 1, l.orderedTitleRefs.Count);
            }
            return (null, 0, 0);
        }

        private static string AttrName(ChronicleDataManager dm, string attrId)
        {
            var def = dm.GetCoreAttribute(attrId);
            return def != null ? def.ResolveDisplayName() : attrId;
        }

        private static string ResolveLadderName(RankLadder l)
        {
            string s = l != null && l.displayName != null ? l.displayName.ResolveText() : null;
            return string.IsNullOrEmpty(s) ? (l != null ? l.id : string.Empty) : s;
        }

        private static string ResolveTreeName(SkillTree t)
        {
            string s = t != null && t.displayName != null ? t.displayName.ResolveText() : null;
            return string.IsNullOrEmpty(s) ? (t != null ? t.id : string.Empty) : s;
        }

        /// <summary>数值格式：最多两位小数，去除多余零。</summary>
        private static string Num(float v) => v.ToString("0.##");

        /// <summary>带符号数值（正数前缀 +）。</summary>
        private static string Signed(float v) => (v >= 0f ? "+" : "") + v.ToString("0.##");

        #endregion
    }
}
