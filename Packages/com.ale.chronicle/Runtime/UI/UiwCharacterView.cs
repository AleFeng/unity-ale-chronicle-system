#if ATK_TMP
using UiText = TMPro.TMP_Text;
#else
using UiText = UnityEngine.UI.Text;
#endif

using System;
using System.Collections.Generic;
using System.Text;
using Ale.Effect;
using Ale.Toolkit.Runtime;
using Ale.Modifier;
using Ale.Toolkit.Runtime.UI;
using UnityEngine;

namespace Ale.Chronicle.Runtime.UI
{
    /// <summary>
    /// 角色信息主界面（MonoBehaviour，继承 <see cref="UiwViewBase"/>，与 <see cref="UiwSkillView"/> 同规格）。
    /// 从 <see cref="ChronicleDataManager"/> 取 <see cref="CharacterDefinition"/>，按「配置 ∪ 运行时」的有效状态分区展示：
    /// 姓名 + 头衔/主职业副标题、个人档案（两列）、6 项能力（数值网格 + 逐来源明细，经 <see cref="ChronicleCharacterRuntime"/>
    /// 汇流特质 / 职业成长 / 头衔 / 条件修改值 / 活动效果 / 永久落地）、特质（临时特质附剩余时长）、效果（活动效果：时长策略 /
    /// 剩余 / 层数 / 修饰器；永久落地汇总）、职业（等级 / 主职业）、头衔（含爵位阶级位次）、以及由职业技能树导出的可用技能 + 已学技能。
    ///
    /// <para>排版参考桌游式角色面板：TMP 富文本做配色 / 分栏 / 层级（无需额外美术图标）。
    /// 打开期间订阅各运行时管理器与世界时钟事件，变化后在 LateUpdate 合并重建一次（同帧多次事件只重建一次）。
    /// 年龄按 <see cref="ChronicleClock"/> 的世界日推算（<see cref="useWorldClock"/> 关闭时用本组件的 <see cref="worldDay"/>）。</para>
    /// </summary>
    public class UiwCharacterView : UiwViewBase
    {
        // ── 配色（TMP 富文本 <color> 十六进制）─────────────────────────────────────
        private const string ColHeader = "#E0C98F"; // 分区标题（暖金）
        private const string ColValue  = "#F0CE73"; // 数值（亮金）
        private const string ColLabel  = "#8B97A9"; // 字段名 / 次要（灰蓝）
        private const string ColName   = "#E6EAF2"; // 主体名（近白）
        private const string ColDim    = "#798394"; // 明细 / 弱化（暗灰）
        private const string ColSub    = "#CBAE7A"; // 副标题（浅金）
        private const string ColAccent = "#8FB4E0"; // 强调标签（浅蓝，如 主职业）
        private const string ColPos    = "#8FCf9a"; // 正加成（绿）
        private const string ColNeg    = "#E08A8A"; // 负加成（红）
        private const string ColTemp   = "#D7B26B"; // 临时 / 剩余时长（琥珀）

        [Header("角色")]
        [Tooltip("要展示的角色 ID（→ ChronicleDataManager.GetCharacter）。")]
        public string characterId = "luna";
        [Tooltip("是否以 ChronicleClock 的世界日计算年龄（推进时钟后自动刷新）；关闭则使用下方 worldDay。")]
        public bool useWorldClock = true;
        [Tooltip("useWorldClock 关闭时用于计算年龄的世界日：年龄 = 世界日 − 出生世界日。")]
        public int worldDay;
        [Tooltip("把「世界日」换算为「岁 / 年」的每年天数（仅用于显示；历法子系统落地前的近似）。")]
        public int daysPerYear = 365;

        [Header("头部")]
        [Tooltip("头像占位：姓名首字（无美术头像时的替身）。")]
        [SerializeField] private UiText portraitInitialText;
        [Tooltip("副标题：最高爵位 · 主职业 Lv.x。")]
        [SerializeField] private UiText subtitleText;
        [Tooltip("元信息：性别 · 年龄。")]
        [SerializeField] private UiText metaText;

        [Header("展示文本（各分区，可按需留空）")]
        [Tooltip("个人档案：性别/年龄/身高/体重/三围/血型/兴趣（两列）。")]
        [SerializeField] private UiText profileLabel;
        [Tooltip("能力：数值网格 + 逐来源明细。")]
        [SerializeField] private UiText attributesLabel;
        [Tooltip("特质（配置 ∪ 运行时；临时特质附剩余时长；含携带的属性修饰器摘要）。")]
        [SerializeField] private UiText traitsLabel;
        [Tooltip("效果：活动效果（策略 / 剩余 / 层数 / 修饰器）+ 永久落地汇总。留空则并入「特质」文本之后。")]
        [SerializeField] private UiText effectsLabel;
        [Tooltip("职业：显示名 + 等级（+ 主职业标记）。")]
        [SerializeField] private UiText professionsLabel;
        [Tooltip("头衔：称号；阶级头衔附带其在阶级序列中的位次。")]
        [SerializeField] private UiText titlesLabel;
        [Tooltip("技能：由角色各职业关联的技能树导出 + 运行时已学。")]
        [SerializeField] private UiText skillsLabel;

        private bool _subscribed;
        private bool _dirty;

        #region 打开 / 关闭

        /// <summary>用当前 <see cref="characterId"/> 打开、订阅运行时事件并构建各分区。</summary>
        public override void Open()
        {
            base.Open();   // 激活面板（公共步骤；先 Unsubscribe）
            Subscribe();
            Rebuild();
        }

        /// <summary>切换角色并（若已打开）刷新。</summary>
        public void Bind(string id)
        {
            characterId = id;
            if (IsOpen) Rebuild();
        }

        /// <summary>手动刷新（数据变动未经事件通知时调用）。</summary>
        public void Refresh()
        {
            if (IsOpen) Rebuild();
        }

        /// <summary>用当前角色重新打开（供基类 <see cref="UiwViewBase.ToggleOpenClose"/>）。</summary>
        protected override void Reopen() => Open();

        private void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;
            EffectRuntimeManager.Instance.OnEffectsChanged        += OnCharacterChanged;
            EffectRuntimeManager.Instance.OnModifiersChanged      += OnCharacterChanged;
            TraitRuntimeManager.Instance.OnTraitsChanged          += OnCharacterChanged;
            TitleRuntimeManager.Instance.OnTitlesChanged          += OnCharacterChanged;
            ProfessionRuntimeManager.Instance.OnProfessionsChanged += OnCharacterChanged;
            SkillRuntimeManager.Instance.OnLearnedChanged         += OnCharacterChanged;
            ChronicleClock.Instance.OnDaysAdvanced                += OnDaysAdvanced;
        }

        /// <summary>退订运行时事件（基类在 Open / Close / OnDestroy 调用）。</summary>
        protected override void Unsubscribe()
        {
            if (!_subscribed) return;
            _subscribed = false;
            EffectRuntimeManager.Instance.OnEffectsChanged        -= OnCharacterChanged;
            EffectRuntimeManager.Instance.OnModifiersChanged      -= OnCharacterChanged;
            TraitRuntimeManager.Instance.OnTraitsChanged          -= OnCharacterChanged;
            TitleRuntimeManager.Instance.OnTitlesChanged          -= OnCharacterChanged;
            ProfessionRuntimeManager.Instance.OnProfessionsChanged -= OnCharacterChanged;
            SkillRuntimeManager.Instance.OnLearnedChanged         -= OnCharacterChanged;
            ChronicleClock.Instance.OnDaysAdvanced                -= OnDaysAdvanced;
        }

        private void OnCharacterChanged(string id)
        {
            if (id == characterId) _dirty = true;
        }

        private void OnDaysAdvanced(int days, int day) => _dirty = true;

        private void LateUpdate()
        {
            if (!_dirty) return;
            _dirty = false;
            if (IsOpen) Rebuild();
        }

        #endregion

        #region 构建

        /// <summary>当前用于年龄推算的世界日。</summary>
        private int CurrentWorldDay => useWorldClock ? ChronicleClock.Instance.WorldDay : worldDay;

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

            // 头部：姓名 / 副标题（爵位·主职业）/ 元信息（性别·年龄）/ 头像占位（姓名首字）
            if (titleLabel)          titleLabel.text          = ResolveName(character);
            if (portraitInitialText) portraitInitialText.text = Initial(character);
            if (subtitleText)        subtitleText.text        = BuildSubtitle(character, dm);
            if (metaText)            metaText.text            = BuildMeta(character, dm);

            string traits  = BuildTraits(character, dm);
            string effects = BuildEffects(character, dm);

            if (profileLabel)     profileLabel.text     = Section("个人档案", BuildProfile(character, dm));
            if (attributesLabel)  attributesLabel.text  = Section("能力",     BuildAttributes(character, dm));
            if (effectsLabel)
            {
                if (traitsLabel) traitsLabel.text = Section("特质", traits);
                effectsLabel.text = Section("效果", effects);
            }
            else if (traitsLabel)
            {
                // 无独立效果节点：效果并入特质文本之后
                traitsLabel.text = Section("特质", traits) + "\n\n" + Section("效果", effects);
            }
            if (professionsLabel) professionsLabel.text = Section("职业",     BuildProfessions(character, dm));
            if (titlesLabel)      titlesLabel.text      = Section("头衔",     BuildTitles(character, dm));
            if (skillsLabel)      skillsLabel.text      = Section("技能",     BuildSkills(character, dm));
        }

        /// <summary>分区标题（暖金加粗 + ◆ 前缀）；内容为空时占位「—」。</summary>
        private static string Section(string header, string body)
            => $"<size=112%><b><color={ColHeader}>◆ {header}</color></b></size>\n"
               + (string.IsNullOrEmpty(body) ? "—" : body);

        private void SetSections(string text)
        {
            if (profileLabel)     profileLabel.text     = text;
            if (attributesLabel)  attributesLabel.text  = text;
            if (traitsLabel)      traitsLabel.text      = text;
            if (effectsLabel)     effectsLabel.text     = text;
            if (professionsLabel) professionsLabel.text = text;
            if (titlesLabel)      titlesLabel.text      = text;
            if (skillsLabel)      skillsLabel.text      = text;
        }

        /// <summary>头像占位文本：姓名首字。</summary>
        private static string Initial(CharacterDefinition c)
        {
            string n = ResolveName(c);
            return string.IsNullOrEmpty(n) ? "?" : n.Substring(0, 1);
        }

        /// <summary>副标题：最高爵位 · 主职业 Lv.x（配置 ∪ 运行时；纯文本，配色由节点设定）。</summary>
        private string BuildSubtitle(CharacterDefinition c, ChronicleDataManager dm)
        {
            TitleDefinition topRank = null;
            foreach (var titleId in ChronicleCharacterRuntime.EffectiveTitleIds(c.id, c))
            {
                var t = dm.GetTitle(titleId);
                if (t != null && t.kind == ETitleKind.RankTitle && (topRank == null || t.rankTier > topRank.rankTier))
                    topRank = t;
            }

            string primary = null;
            var professions = ChronicleCharacterRuntime.EffectiveProfessions(c.id, c);
            foreach (var kv in professions)
            {
                if (!IsPrimaryProfession(c, kv.Key)) continue;
                var p = dm.GetProfession(kv.Key);
                if (p != null) primary = p.ResolveDisplayName() + " Lv." + kv.Value;
                break;
            }

            var parts = new List<string>();
            if (topRank != null) parts.Add(topRank.ResolveDisplayName());
            if (primary != null) parts.Add(primary);
            return string.Join("  ·  ", parts);
        }

        /// <summary>元信息：性别 · 年龄（纯文本，配色由节点设定）。</summary>
        private string BuildMeta(CharacterDefinition c, ChronicleDataManager dm)
        {
            var parts = new List<string>();
            string sex = FieldValue(c, dm, WellKnownAttr.Sex);
            if (!string.IsNullOrEmpty(sex)) parts.Add(sex);
            parts.Add(AgeYears(c) + " 岁");
            return string.Join("  ·  ", parts);
        }

        private int AgeYears(CharacterDefinition c)
        {
            int ageDays = c.GetAge(CurrentWorldDay);
            return daysPerYear > 0 ? ageDays / daysPerYear : ageDays;
        }

        /// <summary>个人档案：两列键值。性别/年龄/身高/体重/三围/血型两两成行，兴趣独占一行。</summary>
        private string BuildProfile(CharacterDefinition c, ChronicleDataManager dm)
        {
            var cells = new List<string>();
            cells.Add(Cell("性别", FieldValue(c, dm, WellKnownAttr.Sex)));
            cells.Add(Cell("年龄", AgeYears(c) + " 岁"));
            cells.Add(Cell("身高", FieldValue(c, dm, WellKnownAttr.Height) + " cm"));
            cells.Add(Cell("体重", FieldValue(c, dm, WellKnownAttr.Weight) + " kg"));
            cells.Add(Cell("三围", FieldValue(c, dm, "measurements")));
            cells.Add(Cell("血型", FieldValue(c, dm, "bloodType")));

            var sb = new StringBuilder();
            for (int i = 0; i < cells.Count; i += 2)
            {
                sb.Append("<pos=0%>").Append(cells[i]);
                if (i + 1 < cells.Count) sb.Append("<pos=50%>").Append(cells[i + 1]);
                sb.Append('\n');
            }
            string interests = FieldValue(c, dm, "interests");
            if (!string.IsNullOrEmpty(interests)) sb.Append(Cell("兴趣", interests));
            return sb.ToString().TrimEnd('\n');
        }

        /// <summary>「标签 值」着色单元（标签灰蓝、值近白）。</summary>
        private static string Cell(string label, string value)
            => $"<color={ColLabel}>{label}</color> <color={ColName}>{value}</color>";

        /// <summary>取字段显示值；枚举经 <see cref="ChronicleDataManager"/> 直解为枚举项名（与 SkillRankUtil 同范式）。</summary>
        private static string FieldValue(CharacterDefinition c, ChronicleDataManager dm, string attrId)
        {
            var av = c.GetAttributeValue(attrId);
            if (av == null) return string.Empty;
            if (av.Type == EFieldType.Enum)
            {
                var et   = dm != null ? dm.GetEnumType(av.EnumTypeRef) : null;
                var item = et != null ? et.GetItemByValue(av.AsEnumValue) : null;
                return item != null ? item.name : av.AsInt.ToString();
            }
            return av.ToDisplayString();
        }

        /// <summary>能力：上半三列「属性名 当前值（金）」网格；下半逐属性「基础→当前 · 各来源明细」（弱化小号）。运行时汇流。</summary>
        private string BuildAttributes(CharacterDefinition c, ChronicleDataManager dm)
        {
            var defs = new List<CoreAttributeDefinition>();
            var evs  = new List<ModifierEvaluation>();
            foreach (var cv in c.coreAttributes)
            {
                var def = dm.GetCoreAttribute(cv.attrId);
                if (def == null) continue;
                defs.Add(def);
                evs.Add(ChronicleCharacterRuntime.Evaluate(c, def));
            }

            var sb = new StringBuilder();
            // 数值网格（3 列）
            for (int i = 0; i < defs.Count; i++)
            {
                int col = i % 3;
                if (col == 0 && i > 0) sb.Append('\n');
                sb.Append("<pos=").Append(col * 34).Append("%>")
                  .Append("<color=").Append(ColLabel).Append('>').Append(defs[i].ResolveDisplayName()).Append("</color> ")
                  .Append("<color=").Append(ColValue).Append("><b>").Append(Num(evs[i].Value)).Append("</b></color>");
            }
            // 明细（弱化小号）
            sb.Append("\n<size=76%><color=").Append(ColDim).Append('>');
            for (int i = 0; i < defs.Count; i++)
            {
                var ev = evs[i];
                sb.Append(defs[i].ResolveDisplayName()).Append(' ')
                  .Append(Num(ev.BaseValue)).Append('→').Append(Num(ev.Value));
                if (!Mathf.Approximately(ev.RawValue, ev.Value))
                    sb.Append("(封顶").Append(Num(ev.RawValue)).Append(')');
                if (ev.Breakdown != null && ev.Breakdown.Count > 0)
                {
                    sb.Append("　");
                    for (int b = 0; b < ev.Breakdown.Count; b++)
                    {
                        if (b > 0) sb.Append('、');
                        var bd = ev.Breakdown[b];
                        sb.Append(PrettySource(bd.SourceTag, dm)).Append(SignedColored(bd.Delta));
                    }
                }
                if (i < defs.Count - 1) sb.Append('\n');
            }
            sb.Append("</color></size>");
            return sb.ToString();
        }

        /// <summary>特质（配置 ∪ 运行时）：名（近白）+ 临时特质剩余时长 / 层数（琥珀）+ 携带的属性修饰器摘要（弱化）。</summary>
        private string BuildTraits(CharacterDefinition c, ChronicleDataManager dm)
        {
            var sb = new StringBuilder();
            bool firstLine = true;
            foreach (var ti in TraitRuntimeManager.Instance.GetEffectiveTraits(c.id, c))
            {
                var t = dm.GetTrait(ti.traitRef);
                if (t == null) continue;
                if (!firstLine) sb.Append('\n');
                firstLine = false;

                sb.Append("<color=").Append(ColName).Append(">· ").Append(t.ResolveDisplayName()).Append("</color>");

                if (!ti.IsPermanent)
                    sb.Append("  <size=85%><color=").Append(ColTemp).Append(">[临时 · 剩余 ").Append(FormatDays(ti.remainingDays)).Append("]</color></size>");
                if (ti.stacks > 1)
                    sb.Append("  <size=85%><color=").Append(ColTemp).Append(">×").Append(ti.stacks).Append("</color></size>");

                if (t.modifiers != null && t.modifiers.Count > 0)
                {
                    var mods = new StringBuilder();
                    bool first = true;
                    foreach (var m in t.modifiers)
                    {
                        if (m == null) continue;
                        if (!first) mods.Append('，');
                        mods.Append(AttrName(dm, m.targetAttributeId)).Append(SignedColored(m.magnitude));
                        first = false;
                    }
                    if (mods.Length > 0)
                        sb.Append("  <size=88%><color=").Append(ColDim).Append(">（").Append(mods).Append("）</color></size>");
                }
            }
            return sb.ToString();
        }

        /// <summary>效果：活动效果逐条（名 / 策略 / 剩余 / 周期 / 层数 / 抑制 / 修饰器摘要）+ 永久落地按属性汇总。</summary>
        private string BuildEffects(CharacterDefinition c, ChronicleDataManager dm)
        {
            var em = EffectRuntimeManager.Instance;
            var sb = new StringBuilder();
            bool firstLine = true;

            foreach (var e in em.GetActiveEffects(c.id))
            {
                if (e == null || !e.IsActive) continue;
                if (!firstLine) sb.Append('\n');
                firstLine = false;

                sb.Append("<color=").Append(ColName).Append(">· ").Append(EffectName(e.Definition, dm)).Append("</color>");

                var meta = new List<string>();
                switch (e.Definition.durationPolicy)
                {
                    case EDurationPolicy.HasDuration: meta.Add("剩余 " + FormatDays(e.Remaining)); break;
                    case EDurationPolicy.Infinite:    meta.Add("永久");                              break;
                    default:                          meta.Add("瞬时");                              break;
                }
                if (e.IsPeriodic) meta.Add("每 " + FormatDays(e.Period) + " 结算");
                if (e.Stacks > 1) meta.Add("×" + e.Stacks);
                if (e.IsInhibited) meta.Add("抑制中");
                sb.Append("  <size=85%><color=").Append(ColTemp).Append(">[").Append(string.Join(" · ", meta)).Append("]</color></size>");

                if (e.ScaledModifiers != null && e.ScaledModifiers.Count > 0)
                {
                    var mods = new StringBuilder();
                    bool first = true;
                    foreach (var m in e.ScaledModifiers)
                    {
                        if (m == null) continue;
                        if (!first) mods.Append('，');
                        mods.Append(AttrName(dm, m.targetAttributeId)).Append(SignedColored(m.magnitude));
                        first = false;
                    }
                    if (mods.Length > 0)
                        sb.Append("  <size=88%><color=").Append(ColDim).Append(">（").Append(mods)
                          .Append(e.IsPeriodic ? "，每次结算永久落地" : "").Append("）</color></size>");
                }
            }

            // 永久落地：按属性汇总加法幅度（其它运算只计次数）
            var permanent = em.GetPermanentModifiers(c.id);
            if (permanent.Count > 0)
            {
                var sums  = new Dictionary<string, float>();
                var order = new List<string>();
                int others = 0;
                foreach (var m in permanent)
                {
                    if (m == null) continue;
                    if (m.operation != EModifierOperation.Add) { others++; continue; }
                    if (!sums.ContainsKey(m.targetAttributeId)) { sums[m.targetAttributeId] = 0f; order.Add(m.targetAttributeId); }
                    sums[m.targetAttributeId] += m.magnitude;
                }
                if (!firstLine) sb.Append('\n');
                sb.Append("<color=").Append(ColSub).Append(">永久落地</color> <size=88%><color=").Append(ColDim).Append('>');
                bool first = true;
                foreach (var id in order)
                {
                    if (!first) sb.Append('，');
                    sb.Append(AttrName(dm, id)).Append(SignedColored(sums[id]));
                    first = false;
                }
                if (others > 0) sb.Append(first ? "" : "，").Append("其它 ").Append(others).Append(" 条");
                sb.Append("</color></size>");
            }
            return sb.ToString();
        }

        /// <summary>职业（配置 ∪ 运行时）：名（近白）+ 等级（金/弱化上限）+ 主职业标签（浅蓝）。</summary>
        private string BuildProfessions(CharacterDefinition c, ChronicleDataManager dm)
        {
            var sb = new StringBuilder();
            bool firstLine = true;
            foreach (var kv in ChronicleCharacterRuntime.EffectiveProfessions(c.id, c))
            {
                var p = dm.GetProfession(kv.Key);
                if (p == null) continue;
                if (!firstLine) sb.Append('\n');
                firstLine = false;

                sb.Append("<color=").Append(ColName).Append(">· ").Append(p.ResolveDisplayName()).Append("</color>")
                  .Append("  <color=").Append(ColValue).Append(">Lv.").Append(kv.Value).Append("</color>")
                  .Append("<color=").Append(ColDim).Append(">/").Append(p.maxLevel).Append("</color>");
                if (IsPrimaryProfession(c, kv.Key))
                    sb.Append("  <size=85%><color=").Append(ColAccent).Append(">[主职业]</color></size>");
            }
            return sb.ToString();
        }

        /// <summary>头衔（配置 ∪ 运行时）：名（近白）；阶级头衔附带其在阶级序列中的位次（弱化）。</summary>
        private string BuildTitles(CharacterDefinition c, ChronicleDataManager dm)
        {
            var sb = new StringBuilder();
            bool firstLine = true;
            foreach (var titleId in ChronicleCharacterRuntime.EffectiveTitleIds(c.id, c))
            {
                var t = dm.GetTitle(titleId);
                if (t == null) continue;
                if (!firstLine) sb.Append('\n');
                firstLine = false;

                sb.Append("<color=").Append(ColName).Append(">· ").Append(t.ResolveDisplayName()).Append("</color>");
                if (t.kind == ETitleKind.RankTitle)
                {
                    var (ladder, pos, total) = FindRankPosition(dm, t.id);
                    if (ladder != null)
                        sb.Append("  <size=85%><color=").Append(ColDim).Append('>')
                          .Append(ResolveLadderName(ladder)).Append(' ').Append(pos).Append('/').Append(total)
                          .Append("</color></size>");
                }
            }
            return sb.ToString();
        }

        /// <summary>技能：按职业技能树分组（树名浅金），列出各技能（近白、去重）；另列运行时已学（永久 ∪ 提供者）。</summary>
        private string BuildSkills(CharacterDefinition c, ChronicleDataManager dm)
        {
            var sb = new StringBuilder();
            var seenTrees = new HashSet<string>();
            bool firstLine = true;
            foreach (var kv in ChronicleCharacterRuntime.EffectiveProfessions(c.id, c))
            {
                var p = dm.GetProfession(kv.Key);
                if (p == null || p.skillTreeRefs == null) continue;
                foreach (var treeId in p.skillTreeRefs)
                {
                    if (string.IsNullOrEmpty(treeId) || !seenTrees.Add(treeId)) continue;
                    var tree = dm.GetSkillTree(treeId);
                    if (tree == null || tree.skills == null) continue;
                    if (!firstLine) sb.Append('\n');
                    firstLine = false;

                    sb.Append("<color=").Append(ColSub).Append(">「").Append(ResolveTreeName(tree)).Append("」</color>");
                    bool first = true;
                    foreach (var e in tree.skills)
                    {
                        if (e == null || string.IsNullOrEmpty(e.skillRef)) continue;
                        var skill = dm.GetSkill(e.skillRef);
                        if (skill == null) continue;
                        sb.Append("<color=").Append(ColName).Append('>').Append(first ? "" : "、")
                          .Append(UiwSkillText.ResolveName(skill)).Append("</color>");
                        first = false;
                    }
                }
            }

            var learned = SkillRuntimeManager.Instance.GetEffectiveSkills(c.id);
            if (learned.Count > 0)
            {
                if (!firstLine) sb.Append('\n');
                sb.Append("<color=").Append(ColSub).Append(">「已学」</color>");
                bool first = true;
                foreach (var skill in learned)
                {
                    sb.Append("<color=").Append(ColName).Append('>').Append(first ? "" : "、")
                      .Append(UiwSkillText.ResolveName(skill)).Append("</color>");
                    first = false;
                }
            }
            return sb.ToString();
        }

        #endregion

        #region 辅助

        /// <summary>主职业判定：配置层标记 或 运行时职业管理器标记。</summary>
        private static bool IsPrimaryProfession(CharacterDefinition c, string professionId)
        {
            if (c.professions != null)
                foreach (var cp in c.professions)
                    if (cp.professionRef == professionId && cp.isPrimary) return true;
            return ProfessionRuntimeManager.Instance.IsPrimary(c.id, professionId);
        }

        /// <summary>角色显示名：读 <see cref="WellKnownAttr.Name"/> 自由字段，空则回退角色 id。</summary>
        private static string ResolveName(CharacterDefinition c)
        {
            string n = c.GetAttributeValue<string>(WellKnownAttr.Name);
            return string.IsNullOrEmpty(n) ? c.id : n;
        }

        /// <summary>效果显示名：toolkit 效果库条目的显示名 → 定义 displayName → id。</summary>
        private static string EffectName(EffectDefinition def, ChronicleDataManager dm)
        {
            if (def == null) return "?";
            var e = EffectDataManager.Instance?.GetEffect(def.id);
            if (e != null) return e.ResolveDisplayName();
            return !string.IsNullOrEmpty(def.displayName) ? def.displayName : def.id;
        }

        /// <summary>
        /// 把修饰器来源标记翻译为可读来源名：trait:{id}→特质名、prof:{id}:growth→职业名+成长、title:{id}→头衔名、
        /// effect:{id}#h→效果名、attr:{id}:cond→条件。
        /// </summary>
        private static string PrettySource(string sourceTag, ChronicleDataManager dm)
        {
            if (string.IsNullOrEmpty(sourceTag)) return "?";

            const string trait = "trait:", title = "title:", prof = "prof:", growth = ":growth", effect = "effect:", attr = "attr:";
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
            if (sourceTag.StartsWith(effect))
            {
                string body = sourceTag.Substring(effect.Length);
                int hash = body.IndexOf('#');
                if (hash >= 0) body = body.Substring(0, hash);
                var e = EffectDataManager.Instance?.GetEffect(body);
                return e != null ? e.ResolveDisplayName() : sourceTag;
            }
            if (sourceTag.StartsWith(attr) && sourceTag.EndsWith(":cond"))
                return "条件";
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

        /// <summary>天数 → 「x 年」（≥ 一年）或「n 天」（向上取整）。</summary>
        private string FormatDays(float days)
        {
            if (days < 0f) return "永久";
            if (daysPerYear > 0 && days >= daysPerYear)
                return (days / daysPerYear).ToString("0.#") + " 年";
            return Mathf.CeilToInt(days) + " 天";
        }

        /// <summary>数值格式：最多两位小数，去除多余零。</summary>
        private static string Num(float v) => v.ToString("0.##");

        /// <summary>带符号且着色的加成数值（正绿负红）。</summary>
        private static string SignedColored(float v)
        {
            string col = v >= 0f ? ColPos : ColNeg;
            string s = (v >= 0f ? "+" : "") + v.ToString("0.##");
            return $"<color={col}>{s}</color>";
        }

        #endregion
    }
}
