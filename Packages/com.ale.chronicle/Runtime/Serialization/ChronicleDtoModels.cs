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
    }
}
