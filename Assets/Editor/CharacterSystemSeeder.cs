#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Ale.Toolkit.Runtime;
using Ale.Condition;

namespace Ale.Chronicle.DemoEditor
{
    /// <summary>
    /// 角色系统 Demo 数据 seeder —— 用代码「全量重建」<see cref="ChronicleDatabase"/>(<see cref="DbPath"/>),
    /// 填入属性 / 特质 / 职业 / 技能 / 技能树 / 头衔 / 阶级序列 + 示例角色。分 D1~D6 步骤,经菜单
    /// (Tools ▸ Ale Toolkit ▸ Chronicle System ▸ Character Seeder)或 MCP execute_code 逐步触发。
    ///
    /// <para>约定:模板键用 name、实例键用 id;引用一律字符串键;先建被引对象、再建引用方;每步末尾 <see cref="ChronicleDatabase.Validate"/> 兜底。
    /// D1 作为重建起点会清空全部列表;后续每步只清空自身负责的列表,故每步独立幂等。</para>
    /// </summary>
    public static partial class CharacterSystemSeeder
    {
        // ── 资产路径 ──────────────────────────────────────────────────────────────
        public const string DbPath = "Assets/Demo/Data/ChronicleDatabase.asset";

        // ── 枚举类型名 ────────────────────────────────────────────────────────────
        public const string EnumSex   = "性别";
        public const string EnumBlood = "血型";

        // ── 模板名(键) ──────────────────────────────────────────────────────────
        public const string TplAbility   = "能力";       // CoreAttributeTemplate
        public const string TplCharacter = "主角";       // CharacterTemplate
        public const string TplTrait     = "性格";       // TraitTemplate
        public const string TplProfession= "战斗职业";   // ProfessionTemplate
        public const string TplSkill     = "主动技能";   // SkillTemplate
        public const string TplTitle     = "世俗爵位";   // TitleTemplate

        // ── 分组标签 id ───────────────────────────────────────────────────────────
        public const string GtCombat = "combat";
        public const string GtMagic  = "magic";
        public const string GtNoble  = "noble";
        public const string GtSocial = "social";
        public const string GtState  = "state";

        // ── 能力属性 id(6 项) ────────────────────────────────────────────────────
        public const string AtMight      = "might";       // 战力
        public const string AtIntellect  = "intellect";   // 智力
        public const string AtStamina    = "stamina";     // 耐力
        public const string AtAgility    = "agility";     // 敏捷
        public const string AtPerception = "perception";  // 感知
        public const string AtCharisma   = "charisma";    // 魅力

        // ── 个人字段 id(WellKnownAttr 之外的自定义字段) ─────────────────────────
        public const string PfMeasurements = "measurements"; // 三围(VectorInt3:胸/腰/臀)
        public const string PfBloodType    = "bloodType";    // 血型(Enum→EnumBlood)
        public const string PfInterests    = "interests";    // 兴趣(String 数组)

        // ── 职业 id(D5 建立;D3 里程碑/等级头衔按此命名,D5 经 LevelUnlock 引用) ─────
        public const string ProfKnight  = "knight";       // 骑士(主职业,0~99)
        public const string ProfMage    = "mage";         // 法师(主职业,0~99)
        public const string ProfScholar = "scholar";      // 学者(主职业,0~99)
        public const string ProfFavor   = "favorability"; // 好感度(0~5)
        public const string ProfMadness = "madness";      // 疯狂度(0~5)

        // ── 职业技能树 id(D4 建立;D5 经 ProfessionDefinition.skillTreeRefs 引用) ────
        public const string KnightTreeId  = "knight_tree";
        public const string MageTreeId    = "mage_tree";
        public const string ScholarTreeId = "scholar_tree";

        // ════════════════════════════════════════════════════════════════════════
        //  D1 · 基础层
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// D1:全量重建起点。清空整库 → 枚举 / 分组标签 / 六系统模板 / 6 个能力核心属性 / 角色个人字段 schema → 校验保存。
        /// 返回 Validate 报告字符串(供 MCP execute_code 回读)。
        /// </summary>
        [MenuItem("Tools/Ale Toolkit/Chronicle System/Character Seeder/D1 基础层")]
        public static string Build_D1()
        {
            var db = GetOrCreateDb();
            ClearAll(db);

            // ── 枚举类型 ──────────────────────────────────────────────────────────
            db.AddEnumType(EnumSex, "男", "女");
            db.AddEnumType(EnumBlood, "A", "B", "O", "AB");

            // ── 分组标签(供后续技能 / 职业 / 头衔按 1 主 + 若干副引用) ──────────────
            db.GroupTags.Add(new ChronicleGroupTag(GtCombat, "战斗"));
            db.GroupTags.Add(new ChronicleGroupTag(GtMagic,  "魔法"));
            db.GroupTags.Add(new ChronicleGroupTag(GtNoble,  "贵族"));
            db.GroupTags.Add(new ChronicleGroupTag(GtSocial, "社交"));
            db.GroupTags.Add(new ChronicleGroupTag(GtState,  "状态"));

            // ── 六系统模板 ────────────────────────────────────────────────────────
            db.CoreAttributeTemplates.Add(new CoreAttributeTemplate(TplAbility)
                { minValue = 0f, maxValue = 100f, defaultBase = 10f });

            var charTpl = new CharacterTemplate(TplCharacter);
            charTpl.attributes.Add(new AttributeDefinition(WellKnownAttr.Name,     EFieldType.String));                 // 姓名
            charTpl.attributes.Add(new AttributeDefinition(WellKnownAttr.Birthday, EFieldType.Int));                    // 生日(世界日;年龄=当前日-该值)
            charTpl.attributes.Add(new AttributeDefinition(WellKnownAttr.Sex,      EFieldType.Enum, false, EnumSex));   // 性别
            charTpl.attributes.Add(new AttributeDefinition(WellKnownAttr.Height,   EFieldType.Float));                  // 身高(cm)
            charTpl.attributes.Add(new AttributeDefinition(WellKnownAttr.Weight,   EFieldType.Float));                  // 体重(kg)
            charTpl.attributes.Add(new AttributeDefinition(PfMeasurements,         EFieldType.VectorInt3));             // 三围(胸/腰/臀)
            charTpl.attributes.Add(new AttributeDefinition(PfBloodType,            EFieldType.Enum, false, EnumBlood)); // 血型
            charTpl.attributes.Add(new AttributeDefinition(PfInterests,            EFieldType.String, true));           // 兴趣(数组)
            db.CharacterTemplates.Add(charTpl);

            db.TraitTemplates.Add(new TraitTemplate(TplTrait));
            db.ProfessionTemplates.Add(new ProfessionTemplate(TplProfession) { maxLevel = 99 });
            db.SkillTemplates.Add(new SkillTemplate(TplSkill));
            db.TitleTemplates.Add(new TitleTemplate(TplTitle) { kind = ETitleKind.RankTitle });

            // ── 6 个能力核心属性 ──────────────────────────────────────────────────
            AddAbility(db, AtMight,      "战力", "战");
            AddAbility(db, AtIntellect,  "智力", "智");
            AddAbility(db, AtStamina,    "耐力", "耐");
            AddAbility(db, AtAgility,    "敏捷", "敏");
            AddAbility(db, AtPerception, "感知", "感");
            AddAbility(db, AtCharisma,   "魅力", "魅");

            // 依模板 schema 对账实例自定义字段
            foreach (var a in db.CoreAttributes) a.RebuildAttributes(db);

            SaveDb(db);
            return ValidateReport(db, "D1",
                $"能力属性={db.CoreAttributes.Count}, 模板[属性{db.CoreAttributeTemplates.Count}/角色{db.CharacterTemplates.Count}/" +
                $"特质{db.TraitTemplates.Count}/职业{db.ProfessionTemplates.Count}/技能{db.SkillTemplates.Count}/头衔{db.TitleTemplates.Count}], " +
                $"枚举={db.EnumTypesList.Count}, 分组标签={db.GroupTags.Count}");
        }

        /// <summary>新增一个能力核心属性(归「能力」模板,0~100,默认基础 10)。</summary>
        private static void AddAbility(ChronicleDatabase db, string id, string displayName, string abbr)
        {
            var a = new CoreAttributeDefinition(id)
            {
                templateRef = TplAbility,
                minValue    = 0f,
                maxValue    = 100f,
                defaultBase = 10f,
            };
            a.displayName.SetTextValue(0, displayName);
            a.abbreviation.SetTextValue(0, abbr);
            db.CoreAttributes.Add(a);
        }

        // ════════════════════════════════════════════════════════════════════════
        //  D2 · 特质
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// D2:生成 6 个特质(均挂「性格」模板、携带对能力属性的修饰器),含 1 组互斥(懦弱↔勇敢)
        /// 与 1 个获得门槛内联条件(睿智:智力≥30)。只清空并重建 <see cref="ChronicleDatabase.Traits"/>。
        /// </summary>
        [MenuItem("Tools/Ale Toolkit/Chronicle System/Character Seeder/D2 特质")]
        public static string Build_D2()
        {
            var db = GetOrCreateDb();
            db.Traits.Clear();

            var brave = AddTrait(db, "brave", "勇敢", "无惧危险,战力与耐力提升。");
            Mod(brave, AtMight, 8f); Mod(brave, AtStamina, 4f);

            var coward = AddTrait(db, "coward", "懦弱", "临阵退缩,战力下降但身法更灵活。");
            Mod(coward, AtMight, -6f); Mod(coward, AtAgility, 3f);
            coward.incompatibleTraitRefs.Add("brave");                 // 显式互斥:与「勇敢」不可并存(双向生效)

            var genius = AddTrait(db, "genius", "天才", "智力超群,兼具敏锐洞察。");
            Mod(genius, AtIntellect, 12f); Mod(genius, AtPerception, 5f);

            var strong = AddTrait(db, "strong", "强壮", "体魄强健,耐力与战力提升。");
            Mod(strong, AtStamina, 10f); Mod(strong, AtMight, 5f);

            var beautiful = AddTrait(db, "beautiful", "美貌", "容颜出众,魅力大增。");
            Mod(beautiful, AtCharisma, 12f);

            var sage = AddTrait(db, "sage", "睿智", "博学通达;需智力达到 30 方可获得。");
            Mod(sage, AtIntellect, 8f); Mod(sage, AtPerception, 4f);
            sage.eligibility = AttrAtLeast(AtIntellect, 30f);          // 获得门槛:主体智力≥30

            foreach (var t in db.Traits) t.RebuildAttributes(db);

            SaveDb(db);
            return ValidateReport(db, "D2", $"特质={db.Traits.Count}(互斥:懦弱↔勇敢;门槛:睿智 智力≥30)");
        }

        /// <summary>新增一个特质(挂「性格」模板、永久),返回以便追加修饰器 / 条件。</summary>
        private static TraitDefinition AddTrait(ChronicleDatabase db, string id, string displayName, string description)
        {
            var t = new TraitDefinition(id) { templateRef = TplTrait, lifetime = ETraitLifetime.Permanent };
            t.displayName.SetTextValue(0, displayName);
            t.description.SetTextValue(0, description);
            db.Traits.Add(t);
            return t;
        }

        /// <summary>给特质追加一条对某能力属性的加法修饰器(汇入 CoreAttributeResolver)。</summary>
        private static void Mod(TraitDefinition t, string attrId, float magnitude)
            => t.modifiers.Add(new ModifierDefinition(attrId, EModifierOperation.Add, magnitude));

        /// <summary>构造「主体某属性 ≥ value」的单项条件表达式(Chronicle.AttributeCompare)。</summary>
        private static ConditionExpression AttrAtLeast(string attrId, float value)
        {
            var item = new ConditionItem("Chronicle.AttributeCompare");
            item.parameters.Add(IntParam("scope",  (int)EConditionScope.Actor));
            item.parameters.Add(StrParam("attrId", attrId));
            item.parameters.Add(IntParam("op",     ChronicleCompareOp.GreaterOrEqual));
            item.parameters.Add(FloatParam("value", value));

            var group = new ConditionGroup { itemOperator = ConditionLogicOp.And };
            group.items.Add(item);

            var expr = new ConditionExpression { groupOperator = ConditionLogicOp.And };
            expr.groups.Add(group);
            return expr;
        }

        private static ConditionParam IntParam(string id, long v)
        {
            var p = new ConditionParam(id, ConditionParamType.Int); p.SetInt(v); return p;
        }

        private static ConditionParam StrParam(string id, string v)
        {
            var p = new ConditionParam(id, ConditionParamType.String); p.SetString(v); return p;
        }

        private static ConditionParam FloatParam(string id, double v)
        {
            var p = new ConditionParam(id, ConditionParamType.Float); p.SetFloat(v); return p;
        }

        // ════════════════════════════════════════════════════════════════════════
        //  D3 · 头衔 + 阶级序列
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// D3:生成头衔与阶级序列。① 爵位五连(RankTitle,rankTier 递增,带魅力修饰器)+ RankLadder「爵位」(低→高);
        /// ② 三个主职业的里程碑称号(Epithet,每 20 级一个);③ 好感/疯狂每级称号(Epithet,1~5 级)。
        /// 只清空并重建 <see cref="ChronicleDatabase.Titles"/> 与 <see cref="ChronicleDatabase.RankLadders"/>。
        /// </summary>
        [MenuItem("Tools/Ale Toolkit/Chronicle System/Character Seeder/D3 头衔+阶级序列")]
        public static string Build_D3()
        {
            var db = GetOrCreateDb();
            db.Titles.Clear();
            db.RankLadders.Clear();

            // ── ① 爵位五连(阶级头衔;越大越高,带魅力加成) ────────────────────────
            AddRank(db, "baron",    "男爵", 1, 2f);
            AddRank(db, "viscount", "子爵", 2, 4f);
            AddRank(db, "count",    "伯爵", 3, 6f);
            AddRank(db, "marquis",  "侯爵", 4, 8f);
            AddRank(db, "duke",     "公爵", 5, 10f);

            var ladder = new RankLadder("nobility");
            ladder.displayName.SetTextValue(0, "爵位");
            ladder.orderedTitleRefs.AddRange(new[] { "baron", "viscount", "count", "marquis", "duke" }); // 低→高
            db.RankLadders.Add(ladder);

            // ── ② 主职业里程碑称号(每 20 级一个;D5 经 LevelUnlock 授予) ────────────
            AddMilestone(db, ProfKnight,  GtCombat, 20, "见习骑士");
            AddMilestone(db, ProfKnight,  GtCombat, 40, "骑士");
            AddMilestone(db, ProfKnight,  GtCombat, 60, "骑士队长");
            AddMilestone(db, ProfKnight,  GtCombat, 80, "圣骑士");
            AddMilestone(db, ProfMage,    GtMagic,  20, "法术学徒");
            AddMilestone(db, ProfMage,    GtMagic,  40, "术士");
            AddMilestone(db, ProfMage,    GtMagic,  60, "大法师");
            AddMilestone(db, ProfMage,    GtMagic,  80, "贤者");
            AddMilestone(db, ProfScholar, GtSocial, 20, "书记员");
            AddMilestone(db, ProfScholar, GtSocial, 40, "学者");
            AddMilestone(db, ProfScholar, GtSocial, 60, "教授");
            AddMilestone(db, ProfScholar, GtSocial, 80, "智者");

            // ── ③ 好感度 / 疯狂度 每级称号(1~5;D5 经 LevelUnlock 授予) ─────────────
            AddLevelTitle(db, FavorTitleId(1), "相识");
            AddLevelTitle(db, FavorTitleId(2), "友好");
            AddLevelTitle(db, FavorTitleId(3), "信赖");
            AddLevelTitle(db, FavorTitleId(4), "挚友");
            AddLevelTitle(db, FavorTitleId(5), "灵魂伴侣");
            AddLevelTitle(db, MadnessTitleId(1), "焦躁");
            AddLevelTitle(db, MadnessTitleId(2), "偏执");
            AddLevelTitle(db, MadnessTitleId(3), "癫狂");
            AddLevelTitle(db, MadnessTitleId(4), "失控");
            AddLevelTitle(db, MadnessTitleId(5), "湮灭");

            foreach (var t in db.Titles) t.RebuildAttributes(db);

            SaveDb(db);
            int ranks = 0, epithets = 0;
            foreach (var t in db.Titles) { if (t.kind == ETitleKind.RankTitle) ranks++; else epithets++; }
            return ValidateReport(db, "D3",
                $"头衔={db.Titles.Count}(阶级{ranks}/称号{epithets}), 阶级序列={db.RankLadders.Count}(爵位:男→公)");
        }

        /// <summary>头衔 id 命名:主职业里程碑 = <c>{profId}_t{level}</c>;好感/疯狂等级 = <c>{profId}_{level}</c>。</summary>
        public static string MilestoneTitleId(string profId, int level) => profId + "_t" + level;
        public static string FavorTitleId(int level)   => ProfFavor + "_" + level;
        public static string MadnessTitleId(int level) => ProfMadness + "_" + level;

        /// <summary>新增一个阶级头衔(爵位,归「世俗爵位」模板、贵族分组、可继承、带魅力加成)。</summary>
        private static void AddRank(ChronicleDatabase db, string id, string name, int tier, float charismaBonus)
        {
            var t = new TitleDefinition(id, TplTitle) { kind = ETitleKind.RankTitle, groupTagRef = GtNoble, rankTier = tier, heritable = true };
            t.displayName.SetTextValue(0, name);
            t.modifiers.Add(new ModifierDefinition(AtCharisma, EModifierOperation.Add, charismaBonus));
            db.Titles.Add(t);
        }

        /// <summary>新增一个职业里程碑称号(Epithet)。</summary>
        private static void AddMilestone(ChronicleDatabase db, string profId, string groupTag, int level, string name)
            => AddEpithet(db, MilestoneTitleId(profId, level), name, groupTag);

        /// <summary>新增一个好感/疯狂等级称号(Epithet,状态分组)。</summary>
        private static void AddLevelTitle(ChronicleDatabase db, string id, string name)
            => AddEpithet(db, id, name, GtState);

        /// <summary>新增一个称号型头衔(Epithet)。</summary>
        private static void AddEpithet(ChronicleDatabase db, string id, string name, string groupTag)
        {
            var t = new TitleDefinition(id) { kind = ETitleKind.Epithet, groupTagRef = groupTag };
            t.displayName.SetTextValue(0, name);
            db.Titles.Add(t);
        }

        // ════════════════════════════════════════════════════════════════════════
        //  D4 · 技能 + 技能树
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// D4:生成 8 个技能 + 5 棵技能树,演示三种类型:
        /// ① 三个主职业的职业技能树(List;D5 经 skillTreeRefs 关联);
        /// ② 剑术(Tiered,三层,层级带战力门槛);③ 火系进阶(Tree,前置链 火球术→烈焰爆→陨星术,末端另加智力门槛)。
        /// 只清空并重建 <see cref="ChronicleDatabase.Skills"/> 与 <see cref="ChronicleDatabase.SkillTrees"/>。
        /// </summary>
        [MenuItem("Tools/Ale Toolkit/Chronicle System/Character Seeder/D4 技能+技能树")]
        public static string Build_D4()
        {
            var db = GetOrCreateDb();
            db.Skills.Clear();
            db.SkillTrees.Clear();

            // ── 技能(均挂「主动技能」模板,主分组标签引用 D1 分组池) ────────────────
            AddSkill(db, "charge",       "冲锋",   "向前突进并对目标造成冲击伤害。", GtCombat); // 骑士职业技能
            AddSkill(db, "fireball",     "火球术", "投掷火球,造成范围火焰伤害。",   GtMagic);  // 法师职业技能 + 火系树根
            AddSkill(db, "erudition",    "博识",   "渊博的学识,提升研究与交涉。",   GtSocial); // 学者职业技能
            AddSkill(db, "slash",        "斩击",   "基础剑术:一次利落的挥斩。",     GtCombat); // 剑术树·初级
            AddSkill(db, "heavy_strike", "重斩",   "蓄力重击,伤害更高。",           GtCombat); // 剑术树·中级
            AddSkill(db, "whirlwind",    "旋风斩", "旋身横扫周围所有敌人。",         GtCombat); // 剑术树·高级
            AddSkill(db, "flame_burst",  "烈焰爆", "火球升级:引爆更大范围的烈焰。", GtMagic);  // 火系树·需火球术
            AddSkill(db, "meteor",       "陨星术", "召唤陨星,毁灭性范围伤害。",     GtMagic);  // 火系树·需烈焰爆

            foreach (var s in db.Skills) s.RebuildAttributes(db);

            // ── ① 职业技能树(List) ───────────────────────────────────────────────
            AddListTree(db, KnightTreeId,  "骑士技能", "charge");
            AddListTree(db, MageTreeId,    "法师技能", "fireball");
            AddListTree(db, ScholarTreeId, "学者技能", "erudition");

            // ── ② 剑术(Tiered):三层,中/高级层带战力门槛 ─────────────────────────
            var sword = new SkillTree("sword_tree") { kind = ESkillTreeKind.Tiered };
            sword.displayName.SetTextValue(0, "剑术");
            sword.tiers.Add(Tier("t1", "初级"));
            sword.tiers.Add(Tier("t2", "中级", AttrAtLeast(AtMight, 30f)));
            sword.tiers.Add(Tier("t3", "高级", AttrAtLeast(AtMight, 60f)));
            sword.skills.Add(TierEntry("slash",        "t1"));
            sword.skills.Add(TierEntry("heavy_strike", "t2"));
            sword.skills.Add(TierEntry("whirlwind",    "t3"));
            db.SkillTrees.Add(sword);

            // ── ③ 火系进阶(Tree):前置链 + 末端额外条件 ───────────────────────────
            var fire = new SkillTree("fire_tree") { kind = ESkillTreeKind.Tree };
            fire.displayName.SetTextValue(0, "火系进阶");
            fire.skills.Add(new SkillTreeEntry { skillRef = "fireball" });        // 根(无前置)
            fire.skills.Add(Prereq("flame_burst", "fireball"));                   // 需:火球术
            var meteorEntry = Prereq("meteor", "flame_burst");                    // 需:烈焰爆
            meteorEntry.unlockCondition = AttrAtLeast(AtIntellect, 50f);          // + 额外门槛:智力≥50
            fire.skills.Add(meteorEntry);
            db.SkillTrees.Add(fire);

            SaveDb(db);
            return ValidateReport(db, "D4", $"技能={db.Skills.Count}, 技能树={db.SkillTrees.Count}(List3/Tiered1/Tree1)");
        }

        /// <summary>新增一个技能(挂「主动技能」模板 + 主分组标签)。</summary>
        private static void AddSkill(ChronicleDatabase db, string id, string name, string desc, string groupTag)
        {
            var s = new Skill(id, TplSkill) { primaryGroupTag = groupTag };
            s.displayText.SetTextValue(0, name);
            s.descriptionText.SetTextValue(0, desc);
            db.Skills.Add(s);
        }

        /// <summary>新增一棵 List 型技能树(一组技能打包,无层级/前置)。</summary>
        private static void AddListTree(ChronicleDatabase db, string id, string name, params string[] skillRefs)
        {
            var tree = new SkillTree(id) { kind = ESkillTreeKind.List };
            tree.displayName.SetTextValue(0, name);
            foreach (var sr in skillRefs) tree.skills.Add(new SkillTreeEntry { skillRef = sr });
            db.SkillTrees.Add(tree);
        }

        /// <summary>构造一个层级(可选解锁条件)。</summary>
        private static SkillTreeTier Tier(string key, string name, ConditionExpression unlock = null)
        {
            var t = new SkillTreeTier { key = key };
            t.displayName.SetTextValue(0, name);
            if (unlock != null) t.unlockCondition = unlock;
            return t;
        }

        /// <summary>层级成员条目(带 tierKey)。</summary>
        private static SkillTreeEntry TierEntry(string skillRef, string tierKey)
            => new SkillTreeEntry { skillRef = skillRef, tierKey = tierKey };

        /// <summary>树状节点条目(带前置技能,AND 语义)。</summary>
        private static SkillTreeEntry Prereq(string skillRef, params string[] prerequisiteSkillRefs)
        {
            var e = new SkillTreeEntry { skillRef = skillRef };
            e.prerequisiteSkillRefs.AddRange(prerequisiteSkillRefs);
            return e;
        }

        // ════════════════════════════════════════════════════════════════════════
        //  D5 · 职业
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// D5:生成 5 个职业。主职业 骑士/法师/学者(maxLevel=99,每级成长汇入属性、每 20 级 LevelUnlock 授里程碑头衔、
        /// 挂 D4 职业技能树;法师另配从业门槛 智力≥15);好感度/疯狂度(maxLevel=5,每级 LevelUnlock 授对应称号)。
        /// 只清空并重建 <see cref="ChronicleDatabase.Professions"/>。
        /// </summary>
        [MenuItem("Tools/Ale Toolkit/Chronicle System/Character Seeder/D5 职业")]
        public static string Build_D5()
        {
            var db = GetOrCreateDb();
            db.Professions.Clear();

            // ── 主职业 骑士(战力/耐力成长;技能树 骑士技能 + 剑术) ─────────────────
            var knight = AddProfession(db, ProfKnight, "骑士", "近战守护者,战力与耐力随等级成长。", GtCombat, 99, TplProfession);
            knight.growth.Add(Growth(AtMight, 1.5f));
            knight.growth.Add(Growth(AtStamina, 1.0f));
            knight.skillTreeRefs.Add(KnightTreeId);
            knight.skillTreeRefs.Add("sword_tree");
            AddMilestoneUnlocks(knight, ProfKnight);

            // ── 主职业 法师(智力/感知成长;技能树 法师技能 + 火系进阶;从业门槛 智力≥15) ──
            var mage = AddProfession(db, ProfMage, "法师", "驾驭元素,智力与感知随等级成长;需智力≥15 方可从业。", GtMagic, 99, TplProfession);
            mage.growth.Add(Growth(AtIntellect, 1.8f));
            mage.growth.Add(Growth(AtPerception, 0.8f));
            mage.skillTreeRefs.Add(MageTreeId);
            mage.skillTreeRefs.Add("fire_tree");
            mage.requirements = AttrAtLeast(AtIntellect, 15f);
            AddMilestoneUnlocks(mage, ProfMage);

            // ── 主职业 学者(智力/魅力成长;技能树 学者技能) ─────────────────────────
            var scholar = AddProfession(db, ProfScholar, "学者", "钻研学问,智力与魅力随等级成长。", GtSocial, 99, TplProfession);
            scholar.growth.Add(Growth(AtIntellect, 1.2f));
            scholar.growth.Add(Growth(AtCharisma, 0.6f));
            scholar.skillTreeRefs.Add(ScholarTreeId);
            AddMilestoneUnlocks(scholar, ProfScholar);

            // ── 好感度 / 疯狂度(点数积累型,maxLevel=5,每级授称号) ───────────────────
            var favor = AddProfession(db, ProfFavor, "好感度", "以点数积累的关系亲密度,每级授予一个称号。", GtState, 5, null);
            for (int lv = 1; lv <= 5; lv++) favor.unlocks.Add(UnlockTitle(lv, FavorTitleId(lv)));

            var madness = AddProfession(db, ProfMadness, "疯狂度", "以点数积累的精神失常度,每级授予一个称号。", GtState, 5, null);
            for (int lv = 1; lv <= 5; lv++) madness.unlocks.Add(UnlockTitle(lv, MadnessTitleId(lv)));

            foreach (var p in db.Professions) p.RebuildAttributes(db);

            SaveDb(db);
            return ValidateReport(db, "D5", $"职业={db.Professions.Count}(主职业3·99级/好感·疯狂2·5级)");
        }

        /// <summary>新增一个职业(挂模板 / 分组 / 等级上限;返回以便追加成长 / 解锁 / 技能树)。</summary>
        private static ProfessionDefinition AddProfession(ChronicleDatabase db, string id, string name, string desc, string groupTag, int maxLevel, string templateRef)
        {
            var p = new ProfessionDefinition(id, templateRef) { groupTagRef = groupTag, maxLevel = maxLevel };
            p.displayName.SetTextValue(0, name);
            p.description.SetTextValue(0, desc);
            db.Professions.Add(p);
            return p;
        }

        /// <summary>一条线性每级成长(perLevel × (level-1))。</summary>
        private static LevelGrowthEntry Growth(string attrId, float perLevel)
            => new LevelGrowthEntry { coreAttrId = attrId, perLevel = perLevel };

        /// <summary>一条「达到 level 级授予 titleId」的解锁。</summary>
        private static LevelUnlock UnlockTitle(int level, string titleId)
        {
            var u = new LevelUnlock { level = level };
            u.grantTitleRefs.Add(titleId);
            return u;
        }

        /// <summary>主职业每 20 级(20/40/60/80)授予对应里程碑称号。</summary>
        private static void AddMilestoneUnlocks(ProfessionDefinition prof, string profId)
        {
            for (int lv = 20; lv <= 80; lv += 20)
                prof.unlocks.Add(UnlockTitle(lv, MilestoneTitleId(profId, lv)));
        }

        // ════════════════════════════════════════════════════════════════════════
        //  共享基础设施
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>加载现有数据库 asset;不存在则在 <see cref="DbPath"/> 新建(保住已引用它的 GUID)。</summary>
        private static ChronicleDatabase GetOrCreateDb()
        {
            var db = AssetDatabase.LoadAssetAtPath<ChronicleDatabase>(DbPath);
            if (db == null)
            {
                var dir = System.IO.Path.GetDirectoryName(DbPath).Replace('\\', '/');
                EnsureFolder(dir);
                db = ScriptableObject.CreateInstance<ChronicleDatabase>();
                AssetDatabase.CreateAsset(db, DbPath);
            }
            return db;
        }

        /// <summary>清空全部 19 个列表(D1 全量重建起点)。</summary>
        private static void ClearAll(ChronicleDatabase db)
        {
            db.EnumTypesList.Clear();
            db.Tags.Clear();
            db.GroupTags.Clear();
            db.NumberFormatConfigs.Clear();
            db.CoreAttributes.Clear();
            db.CoreAttributeTemplates.Clear();
            db.Traits.Clear();
            db.TraitTemplates.Clear();
            db.CharacterTemplates.Clear();
            db.Characters.Clear();
            db.Skills.Clear();
            db.SkillTemplates.Clear();
            db.SkillTrees.Clear();
            db.Professions.Clear();
            db.ProfessionTemplates.Clear();
            db.ProfessionTrees.Clear();
            db.Titles.Clear();
            db.TitleTemplates.Clear();
            db.RankLadders.Clear();
        }

        /// <summary>标脏 + 保存 + 刷新。</summary>
        private static void SaveDb(ChronicleDatabase db)
        {
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        /// <summary>递归确保文件夹存在。</summary>
        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;
            var parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            var leaf   = System.IO.Path.GetFileName(folder);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        /// <summary>跑 Validate,拼装并 Debug.Log 一行报告,返回同一字符串。</summary>
        private static string ValidateReport(ChronicleDatabase db, string step, string counts)
        {
            bool ok = db.Validate(out var errors);
            string result = ok ? "Validate=OK(0 错误)" : ("Validate=失败: " + string.Join(" | ", errors));
            string line = $"[CharacterSystemSeeder] {step} 完成. {counts}. {result}";
            if (ok) Debug.Log(line); else Debug.LogError(line);
            return line;
        }
    }
}
#endif
