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
