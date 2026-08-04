using System;
using Ale.Toolkit.Runtime.Serialization;

namespace Ale.Chronicle.Serialization
{
    /// <summary>
    /// 编年史序列化 DTO：与运行时数据模型一一镜像，Unity 对象引用以 GUID 承载（复用 toolkit 的
    /// <see cref="AttributeValueDto"/> 等）。所有字段 public 且受 JsonUtility / 二进制编解码支持。
    /// 条件表达式（<see cref="TraitDefinitionDto.eligibilityJson"/>）以 Condition System 的 JSON 串承载。
    /// </summary>
    [Serializable]
    public class ChronicleDatabaseDto
    {
        public int version;
        public EnumTypeDto[] enumTypes;
        public TagDto[] tags;
        public CoreAttributeDefinitionDto[] coreAttributes;
        public TraitDefinitionDto[] traits;
        public CharacterTemplateDto[] characterTemplates;
        public CharacterDefinitionDto[] characters;
        // v2 追加
        public CoreAttributeTemplateDto[] coreAttributeTemplates;
        public TraitTemplateDto[] traitTemplates;
        public GroupTagDto[] groupTags;
        public NumberFormatConfigDto[] numberFormatConfigs;
        // v3 追加（技能系统）
        public SkillTemplateDto[] skillTemplates;
        public SkillDto[] skills;
        // v4 追加（职业 / 头衔系统）
        public ProfessionDefinitionDto[] professions;
        public ProfessionTreeDto[] professionTrees;
        public TitleDefinitionDto[] titles;
        public RankLadderDto[] rankLadders;
        // v5 追加（职业 / 头衔模板）
        public ProfessionTemplateDto[] professionTemplates;
        public TitleTemplateDto[] titleTemplates;
    }

    [Serializable]
    public class TagDto
    {
        public string name;
        public AttributeValueDto displayName;
        public AttributeValueDto description;
        public AttributeValueDto backgroundSprite;
        public float[] backgroundColor;
        public bool hideInUI;
        public AttributeDefinitionDto[] attributes;
    }

    [Serializable]
    public class CoreAttributeDefinitionDto
    {
        public string id;
        public string templateRef;                 // v2
        public AttributeValueDto displayName;
        public AttributeValueDto abbreviation;
        public AttributeValueDto description;
        public AttributeValueDto icon;
        public string categoryEnumRef;
        public float minValue;
        public float maxValue;
        public float defaultBase;
        public AttributeEntryDto[] values;         // v2：来自模板 schema 的自定义字段
    }

    /// <summary>属性模板 DTO：派生自 <see cref="ConfigTemplateDto"/>（name/color/attributes），追加默认区间/类别。</summary>
    [Serializable]
    public class CoreAttributeTemplateDto : ConfigTemplateDto
    {
        public string categoryEnumRef;
        public float minValue;
        public float maxValue;
        public float defaultBase;
    }

    [Serializable]
    public class ModifierDefinitionDto
    {
        public string targetAttributeId;
        public int operation;
        public float magnitude;
        public int duration;
        public float durationDays;
        public string sourceTag;
        public int stackLimit;
        public int stackRule;
    }

    [Serializable]
    public class TraitCompatibilityDto
    {
        public string otherTraitRef;
        public float opinionDelta;
    }

    [Serializable]
    public class TraitAiWeightDto
    {
        public string axisRef;
        public float weight;
    }

    [Serializable]
    public class TraitDefinitionDto
    {
        public string id;
        public string templateRef;                 // v2
        public AttributeValueDto displayName;
        public AttributeValueDto description;
        public AttributeValueDto icon;
        public int lifetime;
        public float defaultDurationDays;
        public bool durationStacksRefresh;
        public string categoryEnumRef;
        public string groupEquivalenceRef;
        public string[] incompatibleTraitRefs;
        public ModifierDefinitionDto[] modifiers;
        public string functionTagRef;
        public TraitCompatibilityDto[] compatibilities;
        public bool genetic;
        public float inheritChance;
        public float birthChance;
        public TraitAiWeightDto[] aiWeights;
        /// <summary>eligibility（ConditionExpression）的 Condition System JSON 串。</summary>
        public string eligibilityJson;
        public AttributeEntryDto[] values;         // v2：来自模板 schema 的自定义字段
    }

    /// <summary>特质模板 DTO：派生自 <see cref="ConfigTemplateDto"/>，追加默认类别/时效。</summary>
    [Serializable]
    public class TraitTemplateDto : ConfigTemplateDto
    {
        public string categoryEnumRef;
        public int lifetime;
        public float defaultDurationDays;
    }

    /// <summary>派生自 <see cref="ConfigTemplateDto"/>（name/color/attributes），追加生成规则预留字段。</summary>
    [Serializable]
    public class CharacterTemplateDto : ConfigTemplateDto
    {
        public string raceRef;
        public string[] guaranteedTraitRefs;
        public string[] randomTraitPoolRefs;
        public int attributePointBudget;
        public int minAgeDays;
        public int maxAgeDays;
    }

    /// <summary>技能模板 DTO：派生自 <see cref="ConfigTemplateDto"/>（name/color/attributes），追加技能默认信息。
    /// 图标以 <see cref="AttributeValueDto"/>(Sprite) 承载（与核心属性 / 特质图标同约定）。</summary>
    [Serializable]
    public class SkillTemplateDto : ConfigTemplateDto
    {
        /// <summary>默认显示名（Text：纯文本 fallback + 本地化引用）。</summary>
        public AttributeValueDto displayText;
        /// <summary>默认描述（Text：纯文本 fallback + 本地化引用）。</summary>
        public AttributeValueDto descriptionText;
        /// <summary>默认图标（Sprite 对象类属性值：GUID / Addressable 地址由对象槽承载）。</summary>
        public AttributeValueDto iconValue;
        public string primaryGroupTag;
        public string[] secondaryGroupTags;
    }

    /// <summary>技能实例 DTO。分组标签引用统一 groupTags 池的 id。</summary>
    [Serializable]
    public class SkillDto
    {
        public string id;
        public string templateRef;
        /// <summary>显示名（Text：纯文本 fallback + 本地化引用）。</summary>
        public AttributeValueDto displayText;
        /// <summary>描述（Text：纯文本 fallback + 本地化引用）。</summary>
        public AttributeValueDto descriptionText;
        /// <summary>图标（Sprite 对象类属性值：GUID / Addressable 地址由对象槽承载）。</summary>
        public AttributeValueDto iconValue;
        public string primaryGroupTag;
        public string[] secondaryGroupTags;
        /// <summary>来自模板 schema 的自定义属性值。</summary>
        public AttributeEntryDto[] values;
    }

    [Serializable]
    public class CoreAttributeValueDto
    {
        public string attrId;
        public float baseValue;
    }

    [Serializable]
    public class CharacterTraitInstanceDto
    {
        public string traitRef;
        public float remainingDays;
        public int stacks;
        public string sourceTag;
    }

    [Serializable]
    public class CharacterDefinitionDto
    {
        public string id;
        public string templateRef;
        public AttributeEntryDto[] values;
        public CoreAttributeValueDto[] coreAttributes;
        public CharacterTraitInstanceDto[] traits;
        public string fatherRef;
        public string motherRef;
        public string[] childRefs;
        public CharacterProfessionDto[] professions;   // v4
        public CharacterTitleDto[] titles;             // v4
    }

    // ── v4：职业系统 DTO ─────────────────────────────────────────────────────────

    [Serializable]
    public class ExpCurveDto
    {
        public int mode;
        public float baseExp;
        public float exponent;
        public float linear;
        public int[] perLevelExp;
        public AttributeValueDto curveValue;   // AnimationCurve 承载于 AttributeValue
        public float curveScale;
    }

    [Serializable]
    public class LevelGrowthEntryDto
    {
        public string coreAttrId;
        public float perLevel;
        public bool useCurve;
        public AttributeValueDto levelCurve;   // AnimationCurve
    }

    [Serializable]
    public class LevelUnlockDto
    {
        public int level;
        public string[] grantTraitRefs;
        public string[] grantTitleRefs;
    }

    [Serializable]
    public class ProfessionDefinitionDto
    {
        public string id;
        public string templateRef;                 // v5
        public AttributeValueDto displayName;
        public AttributeValueDto description;
        public AttributeValueDto icon;
        public string groupTagRef;
        public int maxLevel;
        public ExpCurveDto expCurve;
        public LevelGrowthEntryDto[] growth;
        public LevelUnlockDto[] unlocks;
        /// <summary>requirements（ConditionExpression）的 Condition System JSON 串。</summary>
        public string requirementsJson;
        public string[] allowedRaceRefs;
        public AttributeEntryDto[] values;         // v5：来自模板 schema 的自定义字段
    }

    /// <summary>职业模板 DTO：派生自 <see cref="ConfigTemplateDto"/>（name/color/attributes），追加默认预设。</summary>
    [Serializable]
    public class ProfessionTemplateDto : ConfigTemplateDto
    {
        public int maxLevel;
    }

    [Serializable]
    public class ProfessionTreeNodeDto
    {
        public string professionRef;
        public string[] childProfessionRefs;
    }

    [Serializable]
    public class ProfessionTreeDto
    {
        public string id;
        public AttributeValueDto displayName;
        public ProfessionTreeNodeDto[] nodes;
    }

    [Serializable]
    public class CharacterProfessionDto
    {
        public string professionRef;
        public int level;
        public int currentExp;
        public bool isPrimary;
    }

    // ── v4：头衔系统 DTO ─────────────────────────────────────────────────────────

    [Serializable]
    public class TitleDefinitionDto
    {
        public string id;
        public string templateRef;                 // v5
        public AttributeValueDto displayName;
        public AttributeValueDto description;
        public AttributeValueDto icon;
        public int kind;
        public string groupTagRef;
        public int rankTier;
        public bool heritable;
        public string successionPolicyRef;
        public bool isUnique;
        /// <summary>acquisitionConditions（ConditionExpression）的 Condition System JSON 串。</summary>
        public string acquisitionConditionsJson;
        public bool isRevocable;
        public ModifierDefinitionDto[] modifiers;
        public ModifierDefinitionDto[] opinionModifiers;
        public AttributeEntryDto[] values;         // v5：来自模板 schema 的自定义字段
    }

    /// <summary>头衔模板 DTO：派生自 <see cref="ConfigTemplateDto"/>（name/color/attributes），追加默认预设。</summary>
    [Serializable]
    public class TitleTemplateDto : ConfigTemplateDto
    {
        public int kind;
        public bool isRevocable;
    }

    [Serializable]
    public class RankLadderDto
    {
        public string id;
        public AttributeValueDto displayName;
        public string[] orderedTitleRefs;
    }

    [Serializable]
    public class CharacterTitleDto
    {
        public string titleRef;
        public int acquiredWorldDay;
    }
}
