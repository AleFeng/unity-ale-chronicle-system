using System.IO;
using System.Text;
using UnityEngine;
using Ale.Toolkit.Runtime;
using Ale.Toolkit.Runtime.Serialization;
using Ale.Condition;
using static Ale.Toolkit.Runtime.Serialization.ToolkitBinaryCodec;

namespace Ale.Chronicle.Serialization
{
    /// <summary>
    /// 编年史配置序列化器：<see cref="ChronicleDatabase"/> ↔ 紧凑二进制（带魔数 + 版本头）。单向导出格式。
    ///
    /// <para>属性系统 / 枚举 / 属性值条目的紧凑读写复用 toolkit 的 <see cref="ToolkitBinaryCodec"/>；
    /// 对象引用经 <see cref="IAssetRefResolver"/> 转 GUID（运行时用 <see cref="NullAssetRefResolver"/>，
    /// 引用留空、仅存导出 GUID/地址供 Addressable 取用）。条件表达式以 Condition System 的 JSON 串承载。</para>
    ///
    /// <para><b>版本兼容：</b>文件头写魔数 + 版本；读取时按版本号对「后续追加的数据块」做向后兼容跳读
    /// （当前 v1 尚无追加块，扩展点已就位）。</para>
    /// </summary>
    public static class ChronicleConfigSerializer
    {
        /// <summary>魔数 "CHRO"。</summary>
        private const int Magic = 0x4348524F;

        /// <summary>当前序列化格式版本。v2：属性/特质实例追加 templateRef+values，尾部追加 属性模板/特质模板/分组标签/数字格式 四块。</summary>
        public const int Version = 2;

        /// <summary>可正确解析的最低格式版本。</summary>
        private const int MinReadableVersion = 1;

        #region 导出 / 导入（顶层）

        /// <summary>导出为紧凑二进制。<paramref name="resolver"/> 为 null 时用 <see cref="NullAssetRefResolver"/>。</summary>
        public static byte[] Export(ChronicleDatabase db, IAssetRefResolver resolver = null)
        {
            resolver ??= NullAssetRefResolver.Instance;
            var dto = ToDto(db, resolver);

            using var stream = new MemoryStream();
            using (var w = new BinaryWriter(stream, Encoding.UTF8))
            {
                w.Write(Magic);
                w.Write(Version);

                WriteArray(w, dto.enumTypes, WriteEnumType);
                WriteArray(w, dto.tags, WriteTag);
                WriteArray(w, dto.coreAttributes, WriteCoreAttribute);
                WriteArray(w, dto.traits, WriteTrait);
                WriteArray(w, dto.characterTemplates, WriteCharacterTemplate);
                WriteArray(w, dto.characters, WriteCharacter);

                // v2 追加块（读取端按版本号跳读以兼容旧文件）
                WriteArray(w, dto.coreAttributeTemplates, WriteCoreAttributeTemplate);
                WriteArray(w, dto.traitTemplates, WriteTraitTemplate);
                WriteArray(w, dto.groupTags, WriteGroupTag);
                WriteArray(w, dto.numberFormatConfigs, WriteNumberFormatConfig);
            }
            return stream.ToArray();
        }

        /// <summary>导入为一个新的 <see cref="ChronicleDatabase"/> 实例。</summary>
        public static ChronicleDatabase Import(byte[] bytes, IAssetRefResolver resolver = null)
        {
            var db = ScriptableObject.CreateInstance<ChronicleDatabase>();
            ImportInto(bytes, db, resolver);
            return db;
        }

        /// <summary>把二进制导入到既有数据库（先清空其内容）。</summary>
        public static void ImportInto(byte[] bytes, ChronicleDatabase target, IAssetRefResolver resolver = null)
        {
            if (bytes == null || bytes.Length < 8 || !target) return;
            resolver ??= NullAssetRefResolver.Instance;

            using var stream = new MemoryStream(bytes);
            using var r = new BinaryReader(stream, Encoding.UTF8);

            int magic = r.ReadInt32();
            if (magic != Magic)
            {
                Debug.LogError("[ChronicleConfigSerializer] 魔数不匹配，数据格式无效。");
                return;
            }

            int version = r.ReadInt32();
            if (version > Version)
                Debug.LogWarning($"[ChronicleConfigSerializer] 文件版本（{version}）高于当前支持（{Version}），尝试按当前格式解析。");
            else if (version < MinReadableVersion)
                Debug.LogWarning($"[ChronicleConfigSerializer] 文件版本过旧（{version}，最低 {MinReadableVersion}），建议重新导出。");

            var dto = new ChronicleDatabaseDto
            {
                version            = version,
                enumTypes          = ReadArray(r, ReadEnumType),
                tags               = ReadArray(r, ReadTag),
                coreAttributes     = ReadArray(r, br => ReadCoreAttribute(br, version)),
                traits             = ReadArray(r, br => ReadTrait(br, version)),
                characterTemplates = ReadArray(r, ReadCharacterTemplate),
                characters         = ReadArray(r, ReadCharacter),
            };

            // v2 追加块：旧 v1 文件到 characters 即结束，按版本号跳读兼容。
            if (version >= 2)
            {
                dto.coreAttributeTemplates = ReadArray(r, ReadCoreAttributeTemplate);
                dto.traitTemplates         = ReadArray(r, ReadTraitTemplate);
                dto.groupTags              = ReadArray(r, ReadGroupTag);
                dto.numberFormatConfigs    = ReadArray(r, ReadNumberFormatConfig);
            }

            FromDto(dto, target, resolver);
        }

        #endregion

        #region 映射：运行时 ↔ DTO

        private static ChronicleDatabaseDto ToDto(ChronicleDatabase db, IAssetRefResolver resolver)
        {
            return new ChronicleDatabaseDto
            {
                version            = Version,
                enumTypes          = ToolkitDtoMapper.ToArray(db.EnumTypesList, e => ToDto(e, resolver)),
                tags               = ToolkitDtoMapper.ToArray(db.Tags, t => ToDto(t, resolver)),
                coreAttributes     = ToolkitDtoMapper.ToArray(db.CoreAttributes, a => ToDto(a, resolver)),
                traits             = ToolkitDtoMapper.ToArray(db.Traits, t => ToDto(t, resolver)),
                characterTemplates = ToolkitDtoMapper.ToArray(db.CharacterTemplates, t => ToDto(t, resolver)),
                characters         = ToolkitDtoMapper.ToArrayFiltered(db.Characters,
                                        c => c != null && !string.IsNullOrWhiteSpace(c.id), c => ToDto(c, resolver)),
                coreAttributeTemplates = ToolkitDtoMapper.ToArray(db.CoreAttributeTemplates, t => ToDto(t, resolver)),
                traitTemplates         = ToolkitDtoMapper.ToArray(db.TraitTemplates, t => ToDto(t, resolver)),
                groupTags              = ToolkitDtoMapper.ToArray(db.GroupTags, g => ToolkitDtoMapper.ToDto(g, resolver)),
                numberFormatConfigs    = ToolkitDtoMapper.ToArray(db.NumberFormatConfigs, c => ToDto(c, resolver)),
            };
        }

        private static void FromDto(ChronicleDatabaseDto dto, ChronicleDatabase target, IAssetRefResolver resolver)
        {
            target.EnumTypesList.Clear();
            target.Tags.Clear();
            target.CoreAttributes.Clear();
            target.Traits.Clear();
            target.CharacterTemplates.Clear();
            target.Characters.Clear();
            target.CoreAttributeTemplates.Clear();
            target.TraitTemplates.Clear();
            target.GroupTags.Clear();
            target.NumberFormatConfigs.Clear();
            if (dto == null) return;

            if (dto.enumTypes != null)          foreach (var e in dto.enumTypes)          target.EnumTypesList.Add(FromDto(e, resolver));
            if (dto.tags != null)               foreach (var t in dto.tags)               target.Tags.Add(FromDto(t, resolver));
            if (dto.coreAttributes != null)     foreach (var a in dto.coreAttributes)     target.CoreAttributes.Add(FromDto(a, resolver));
            if (dto.traits != null)             foreach (var t in dto.traits)             target.Traits.Add(FromDto(t, resolver));
            if (dto.characterTemplates != null) foreach (var t in dto.characterTemplates) target.CharacterTemplates.Add(FromDto(t, resolver));
            if (dto.characters != null)         foreach (var c in dto.characters)         target.Characters.Add(FromDto(c, resolver));
            if (dto.coreAttributeTemplates != null) foreach (var t in dto.coreAttributeTemplates) target.CoreAttributeTemplates.Add(FromDto(t, resolver));
            if (dto.traitTemplates != null)         foreach (var t in dto.traitTemplates)         target.TraitTemplates.Add(FromDto(t, resolver));
            if (dto.groupTags != null)              foreach (var g in dto.groupTags)              target.GroupTags.Add(ToolkitDtoMapper.FromDto<ChronicleGroupTag>(g, resolver));
            if (dto.numberFormatConfigs != null)    foreach (var c in dto.numberFormatConfigs)    target.NumberFormatConfigs.Add(FromDto(c, resolver));
        }

        // ── 枚举类型 ────────────────────────────────────────────────────────────────

        private static EnumTypeDto ToDto(EnumType e, IAssetRefResolver resolver)
        {
            return new EnumTypeDto
            {
                name       = e.name,
                nextValue  = e.nextValue,
                attributes = ToolkitDtoMapper.ToArray(e.attributes, a => ToolkitDtoMapper.ToDto(a, resolver)),
                items = ToolkitDtoMapper.ToArray(e.items, it => new EnumItemDto
                {
                    name  = it.name,
                    value = it.value,
                    attributeValues = ToolkitDtoMapper.ToDto(it.attributeValues, resolver)
                })
            };
        }

        private static EnumType FromDto(EnumTypeDto dto, IAssetRefResolver resolver)
        {
            var e = new EnumType(dto.name) { nextValue = dto.nextValue };
            if (dto.attributes != null)
                foreach (var a in dto.attributes) e.attributes.Add(ToolkitDtoMapper.FromDto(a, resolver));
            if (dto.items != null)
                foreach (var it in dto.items)
                {
                    var item = new EnumItem(it.name, it.value);
                    ToolkitDtoMapper.FromDto(it.attributeValues, item.attributeValues, resolver);
                    e.items.Add(item);
                }
            return e;
        }

        // ── 功能标签 ────────────────────────────────────────────────────────────────

        private static TagDto ToDto(Tag t, IAssetRefResolver resolver)
        {
            return new TagDto
            {
                name             = t.name,
                displayName      = ToolkitDtoMapper.ToDto(t.displayNameText, resolver),
                description      = ToolkitDtoMapper.ToDto(t.descriptionText, resolver),
                backgroundSprite = ToolkitDtoMapper.ToDto(t.backgroundSpriteValue, resolver),
                backgroundColor  = ToolkitDtoMapper.ToDto(t.backgroundColor),
                hideInUI         = t.hideInUI,
                attributes       = ToolkitDtoMapper.ToArray(t.attributes, a => ToolkitDtoMapper.ToDto(a, resolver)),
            };
        }

        private static Tag FromDto(TagDto dto, IAssetRefResolver resolver)
        {
            var t = new Tag(dto.name)
            {
                displayNameText       = ToolkitDtoMapper.TextFromDto(dto.displayName, resolver),
                descriptionText       = ToolkitDtoMapper.TextFromDto(dto.description, resolver),
                backgroundSpriteValue = dto.backgroundSprite != null ? ToolkitDtoMapper.FromDto(dto.backgroundSprite, resolver) : new AttributeValue(EFieldType.Sprite),
                backgroundColor       = ToolkitDtoMapper.FromDto(dto.backgroundColor, Color.white),
                hideInUI              = dto.hideInUI,
            };
            if (dto.attributes != null)
                foreach (var a in dto.attributes) t.attributes.Add(ToolkitDtoMapper.FromDto(a, resolver));
            return t;
        }

        // ── 核心属性 ────────────────────────────────────────────────────────────────

        private static CoreAttributeDefinitionDto ToDto(CoreAttributeDefinition d, IAssetRefResolver resolver)
        {
            return new CoreAttributeDefinitionDto
            {
                id              = d.id,
                templateRef     = d.templateRef,
                displayName     = ToolkitDtoMapper.ToDto(d.displayName, resolver),
                abbreviation    = ToolkitDtoMapper.ToDto(d.abbreviation, resolver),
                description     = ToolkitDtoMapper.ToDto(d.description, resolver),
                icon            = ToolkitDtoMapper.ToDto(d.icon, resolver),
                categoryEnumRef = d.categoryEnumRef,
                minValue        = d.minValue,
                maxValue        = d.maxValue,
                defaultBase     = d.defaultBase,
                values          = ToolkitDtoMapper.ToDto(d.values, resolver),
            };
        }

        private static CoreAttributeDefinition FromDto(CoreAttributeDefinitionDto dto, IAssetRefResolver resolver)
        {
            var d = new CoreAttributeDefinition
            {
                id              = dto.id,
                templateRef     = dto.templateRef,
                displayName     = ToolkitDtoMapper.TextFromDto(dto.displayName, resolver),
                abbreviation    = ToolkitDtoMapper.TextFromDto(dto.abbreviation, resolver),
                description     = ToolkitDtoMapper.TextFromDto(dto.description, resolver),
                icon            = dto.icon != null ? ToolkitDtoMapper.FromDto(dto.icon, resolver) : new AttributeValue(EFieldType.Sprite),
                categoryEnumRef = dto.categoryEnumRef,
                minValue        = dto.minValue,
                maxValue        = dto.maxValue,
                defaultBase     = dto.defaultBase,
            };
            ToolkitDtoMapper.FromDto(dto.values, d.values, resolver);
            d.InvalidateEntryCache();
            d.Normalize();
            return d;
        }

        // ── 修饰器 ──────────────────────────────────────────────────────────────────

        private static ModifierDefinitionDto ToDto(ModifierDefinition m)
        {
            return new ModifierDefinitionDto
            {
                targetAttributeId = m.targetAttributeId,
                operation         = (int)m.operation,
                magnitude         = m.magnitude,
                duration          = (int)m.duration,
                durationDays      = m.durationDays,
                sourceTag         = m.sourceTag,
                stackLimit        = m.stackLimit,
                stackRule         = (int)m.stackRule,
            };
        }

        private static ModifierDefinition FromDto(ModifierDefinitionDto dto)
        {
            return new ModifierDefinition
            {
                targetAttributeId = dto.targetAttributeId,
                operation         = (EModifierOperation)dto.operation,
                magnitude         = dto.magnitude,
                duration          = (EModifierDuration)dto.duration,
                durationDays      = dto.durationDays,
                sourceTag         = dto.sourceTag,
                stackLimit        = dto.stackLimit,
                stackRule         = (EStackRule)dto.stackRule,
            };
        }

        // ── 特质 ────────────────────────────────────────────────────────────────────

        private static TraitDefinitionDto ToDto(TraitDefinition t, IAssetRefResolver resolver)
        {
            return new TraitDefinitionDto
            {
                id                    = t.id,
                templateRef           = t.templateRef,
                displayName           = ToolkitDtoMapper.ToDto(t.displayName, resolver),
                description           = ToolkitDtoMapper.ToDto(t.description, resolver),
                icon                  = ToolkitDtoMapper.ToDto(t.icon, resolver),
                lifetime              = (int)t.lifetime,
                defaultDurationDays   = t.defaultDurationDays,
                durationStacksRefresh = t.durationStacksRefresh,
                categoryEnumRef       = t.categoryEnumRef,
                groupEquivalenceRef   = t.groupEquivalenceRef,
                incompatibleTraitRefs = ToolkitDtoMapper.ToArray(t.incompatibleTraitRefs),
                modifiers             = ToolkitDtoMapper.ToArray(t.modifiers, ToDto),
                functionTagRef        = t.functionTagRef,
                compatibilities       = ToolkitDtoMapper.ToArray(t.compatibilities,
                                            c => new TraitCompatibilityDto { otherTraitRef = c.otherTraitRef, opinionDelta = c.opinionDelta }),
                genetic               = t.genetic,
                inheritChance         = t.inheritChance,
                birthChance           = t.birthChance,
                aiWeights             = ToolkitDtoMapper.ToArray(t.aiWeights,
                                            a => new TraitAiWeightDto { axisRef = a.axisRef, weight = a.weight }),
                eligibilityJson       = ConditionJson.ToJson(t.eligibility ?? new ConditionExpression(), pretty: false),
                values                = ToolkitDtoMapper.ToDto(t.values, resolver),
            };
        }

        private static TraitDefinition FromDto(TraitDefinitionDto dto, IAssetRefResolver resolver)
        {
            var t = new TraitDefinition
            {
                id                    = dto.id,
                templateRef           = dto.templateRef,
                displayName           = ToolkitDtoMapper.TextFromDto(dto.displayName, resolver),
                description           = ToolkitDtoMapper.TextFromDto(dto.description, resolver),
                icon                  = dto.icon != null ? ToolkitDtoMapper.FromDto(dto.icon, resolver) : new AttributeValue(EFieldType.Sprite),
                lifetime              = (ETraitLifetime)dto.lifetime,
                defaultDurationDays   = dto.defaultDurationDays,
                durationStacksRefresh = dto.durationStacksRefresh,
                categoryEnumRef       = dto.categoryEnumRef,
                groupEquivalenceRef   = dto.groupEquivalenceRef,
                incompatibleTraitRefs = ToolkitDtoMapper.FromDto(dto.incompatibleTraitRefs),
                functionTagRef        = dto.functionTagRef,
                genetic               = dto.genetic,
                inheritChance         = dto.inheritChance,
                birthChance           = dto.birthChance,
                eligibility           = ConditionJson.FromJson(dto.eligibilityJson),
            };
            if (dto.modifiers != null)
                foreach (var m in dto.modifiers) t.modifiers.Add(FromDto(m));
            if (dto.compatibilities != null)
                foreach (var c in dto.compatibilities) t.compatibilities.Add(new TraitCompatibility(c.otherTraitRef, c.opinionDelta));
            if (dto.aiWeights != null)
                foreach (var a in dto.aiWeights) t.aiWeights.Add(new TraitAiWeight(a.axisRef, a.weight));
            ToolkitDtoMapper.FromDto(dto.values, t.values, resolver);
            t.InvalidateEntryCache();
            t.Normalize();
            return t;
        }

        // ── 属性模板 / 特质模板 / 数字格式 ─────────────────────────────────────────────

        private static CoreAttributeTemplateDto ToDto(CoreAttributeTemplate t, IAssetRefResolver resolver)
        {
            var dto = new CoreAttributeTemplateDto
            {
                categoryEnumRef = t.categoryEnumRef,
                minValue        = t.minValue,
                maxValue        = t.maxValue,
                defaultBase     = t.defaultBase,
            };
            ToolkitDtoMapper.FillTemplateDto(dto, t, resolver);
            return dto;
        }

        private static CoreAttributeTemplate FromDto(CoreAttributeTemplateDto dto, IAssetRefResolver resolver)
        {
            var t = new CoreAttributeTemplate();
            ToolkitDtoMapper.FillTemplate(t, dto, resolver);
            t.categoryEnumRef = dto.categoryEnumRef;
            t.minValue        = dto.minValue;
            t.maxValue        = dto.maxValue;
            t.defaultBase     = dto.defaultBase;
            return t;
        }

        private static TraitTemplateDto ToDto(TraitTemplate t, IAssetRefResolver resolver)
        {
            var dto = new TraitTemplateDto
            {
                categoryEnumRef     = t.categoryEnumRef,
                lifetime            = (int)t.lifetime,
                defaultDurationDays = t.defaultDurationDays,
            };
            ToolkitDtoMapper.FillTemplateDto(dto, t, resolver);
            return dto;
        }

        private static TraitTemplate FromDto(TraitTemplateDto dto, IAssetRefResolver resolver)
        {
            var t = new TraitTemplate();
            ToolkitDtoMapper.FillTemplate(t, dto, resolver);
            t.categoryEnumRef     = dto.categoryEnumRef;
            t.lifetime            = (ETraitLifetime)dto.lifetime;
            t.defaultDurationDays = dto.defaultDurationDays;
            return t;
        }

        private static NumberFormatConfigDto ToDto(NumberFormatConfig c, IAssetRefResolver resolver)
        {
            return new NumberFormatConfigDto
            {
                name = c.name,
                locales = ToolkitDtoMapper.ToArray(c.locales, loc => new NumberFormatLocaleDto
                {
                    languageCode = loc.languageCode,
                    rules = ToolkitDtoMapper.ToArray(loc.rules, r => new NumberFormatRuleDto
                    {
                        threshold     = r.threshold,
                        divisor       = r.divisor,
                        suffixText    = ToolkitDtoMapper.ToDto(r.suffixText, resolver),
                        decimalPlaces = r.decimalPlaces,
                    })
                })
            };
        }

        private static NumberFormatConfig FromDto(NumberFormatConfigDto dto, IAssetRefResolver resolver)
        {
            var c = new NumberFormatConfig { name = dto.name };
            if (dto.locales == null) return c;
            foreach (var locDto in dto.locales)
            {
                var loc = new NumberFormatLocale { languageCode = locDto.languageCode };
                if (locDto.rules != null)
                    foreach (var r in locDto.rules)
                        loc.rules.Add(new NumberFormatRule
                        {
                            threshold     = r.threshold,
                            divisor       = r.divisor,
                            suffixText    = ToolkitDtoMapper.TextFromDto(r.suffixText, resolver),
                            decimalPlaces = r.decimalPlaces,
                        });
                c.locales.Add(loc);
            }
            return c;
        }

        // ── 角色模板 ────────────────────────────────────────────────────────────────

        private static CharacterTemplateDto ToDto(CharacterTemplate t, IAssetRefResolver resolver)
        {
            var dto = new CharacterTemplateDto
            {
                raceRef              = t.raceRef,
                guaranteedTraitRefs  = ToolkitDtoMapper.ToArray(t.guaranteedTraitRefs),
                randomTraitPoolRefs  = ToolkitDtoMapper.ToArray(t.randomTraitPoolRefs),
                attributePointBudget = t.attributePointBudget,
                minAgeDays           = t.minAgeDays,
                maxAgeDays           = t.maxAgeDays,
            };
            ToolkitDtoMapper.FillTemplateDto(dto, t, resolver);   // name / color / attributes
            return dto;
        }

        private static CharacterTemplate FromDto(CharacterTemplateDto dto, IAssetRefResolver resolver)
        {
            var t = new CharacterTemplate();
            ToolkitDtoMapper.FillTemplate(t, dto, resolver);
            t.raceRef              = dto.raceRef;
            t.guaranteedTraitRefs  = ToolkitDtoMapper.FromDto(dto.guaranteedTraitRefs);
            t.randomTraitPoolRefs  = ToolkitDtoMapper.FromDto(dto.randomTraitPoolRefs);
            t.attributePointBudget = dto.attributePointBudget;
            t.minAgeDays           = dto.minAgeDays;
            t.maxAgeDays           = dto.maxAgeDays;
            return t;
        }

        // ── 角色 ────────────────────────────────────────────────────────────────────

        private static CharacterDefinitionDto ToDto(CharacterDefinition c, IAssetRefResolver resolver)
        {
            return new CharacterDefinitionDto
            {
                id             = c.id,
                templateRef    = c.templateRef,
                values         = ToolkitDtoMapper.ToDto(c.values, resolver),
                coreAttributes = ToolkitDtoMapper.ToArray(c.coreAttributes,
                                    cv => new CoreAttributeValueDto { attrId = cv.attrId, baseValue = cv.baseValue }),
                traits         = ToolkitDtoMapper.ToArray(c.traits,
                                    ti => new CharacterTraitInstanceDto { traitRef = ti.traitRef, remainingDays = ti.remainingDays, stacks = ti.stacks, sourceTag = ti.sourceTag }),
                fatherRef      = c.fatherRef,
                motherRef      = c.motherRef,
                childRefs      = ToolkitDtoMapper.ToArray(c.childRefs),
            };
        }

        private static CharacterDefinition FromDto(CharacterDefinitionDto dto, IAssetRefResolver resolver)
        {
            var c = new CharacterDefinition(dto.id, dto.templateRef);
            ToolkitDtoMapper.FromDto(dto.values, c.values, resolver);
            if (dto.coreAttributes != null)
                foreach (var cv in dto.coreAttributes) c.coreAttributes.Add(new CoreAttributeValue(cv.attrId, cv.baseValue));
            if (dto.traits != null)
                foreach (var ti in dto.traits) c.traits.Add(new CharacterTraitInstance(ti.traitRef, ti.remainingDays, ti.stacks, ti.sourceTag));
            c.fatherRef = dto.fatherRef;
            c.motherRef = dto.motherRef;
            c.childRefs = ToolkitDtoMapper.FromDto(dto.childRefs);
            c.InvalidateEntryCache();
            return c;
        }

        #endregion

        #region 二进制读写（DTO ↔ 字节流）

        private static void WriteTag(BinaryWriter w, TagDto t)
        {
            WriteStr(w, t.name);
            WriteValue(w, t.displayName);
            WriteValue(w, t.description);
            WriteValue(w, t.backgroundSprite);
            WriteFloatArray(w, t.backgroundColor);
            w.Write(t.hideInUI);
            WriteArray(w, t.attributes, WriteDefinition);
        }

        private static TagDto ReadTag(BinaryReader r)
        {
            return new TagDto
            {
                name             = ReadStr(r),
                displayName      = ReadValue(r),
                description      = ReadValue(r),
                backgroundSprite = ReadValue(r),
                backgroundColor  = ReadFloatArray(r),
                hideInUI         = r.ReadBoolean(),
                attributes       = ReadArray(r, ReadDefinition),
            };
        }

        private static void WriteCoreAttribute(BinaryWriter w, CoreAttributeDefinitionDto d)
        {
            WriteStr(w, d.id);
            WriteValue(w, d.displayName);
            WriteValue(w, d.abbreviation);
            WriteValue(w, d.description);
            WriteValue(w, d.icon);
            WriteStr(w, d.categoryEnumRef);
            w.Write(d.minValue);
            w.Write(d.maxValue);
            w.Write(d.defaultBase);
            WriteStr(w, d.templateRef);   // v2
            WriteEntries(w, d.values);    // v2
        }

        private static CoreAttributeDefinitionDto ReadCoreAttribute(BinaryReader r, int version)
        {
            var dto = new CoreAttributeDefinitionDto
            {
                id              = ReadStr(r),
                displayName     = ReadValue(r),
                abbreviation    = ReadValue(r),
                description     = ReadValue(r),
                icon            = ReadValue(r),
                categoryEnumRef = ReadStr(r),
                minValue        = r.ReadSingle(),
                maxValue        = r.ReadSingle(),
                defaultBase     = r.ReadSingle(),
            };
            if (version >= 2)
            {
                dto.templateRef = ReadStr(r);
                dto.values      = ReadEntries(r);
            }
            return dto;
        }

        private static void WriteModifier(BinaryWriter w, ModifierDefinitionDto m)
        {
            WriteStr(w, m.targetAttributeId);
            w.Write(m.operation);
            w.Write(m.magnitude);
            w.Write(m.duration);
            w.Write(m.durationDays);
            WriteStr(w, m.sourceTag);
            w.Write(m.stackLimit);
            w.Write(m.stackRule);
        }

        private static ModifierDefinitionDto ReadModifier(BinaryReader r)
        {
            return new ModifierDefinitionDto
            {
                targetAttributeId = ReadStr(r),
                operation         = r.ReadInt32(),
                magnitude         = r.ReadSingle(),
                duration          = r.ReadInt32(),
                durationDays      = r.ReadSingle(),
                sourceTag         = ReadStr(r),
                stackLimit        = r.ReadInt32(),
                stackRule         = r.ReadInt32(),
            };
        }

        private static void WriteTrait(BinaryWriter w, TraitDefinitionDto t)
        {
            WriteStr(w, t.id);
            WriteValue(w, t.displayName);
            WriteValue(w, t.description);
            WriteValue(w, t.icon);
            w.Write(t.lifetime);
            w.Write(t.defaultDurationDays);
            w.Write(t.durationStacksRefresh);
            WriteStr(w, t.categoryEnumRef);
            WriteStr(w, t.groupEquivalenceRef);
            WriteStrArray(w, t.incompatibleTraitRefs);
            WriteArray(w, t.modifiers, WriteModifier);
            WriteStr(w, t.functionTagRef);
            WriteArray(w, t.compatibilities, (bw, c) => { WriteStr(bw, c.otherTraitRef); bw.Write(c.opinionDelta); });
            w.Write(t.genetic);
            w.Write(t.inheritChance);
            w.Write(t.birthChance);
            WriteArray(w, t.aiWeights, (bw, a) => { WriteStr(bw, a.axisRef); bw.Write(a.weight); });
            WriteStr(w, t.eligibilityJson);
            WriteStr(w, t.templateRef);   // v2
            WriteEntries(w, t.values);    // v2
        }

        private static TraitDefinitionDto ReadTrait(BinaryReader r, int version)
        {
            var dto = new TraitDefinitionDto
            {
                id                    = ReadStr(r),
                displayName           = ReadValue(r),
                description           = ReadValue(r),
                icon                  = ReadValue(r),
                lifetime              = r.ReadInt32(),
                defaultDurationDays   = r.ReadSingle(),
                durationStacksRefresh = r.ReadBoolean(),
                categoryEnumRef       = ReadStr(r),
                groupEquivalenceRef   = ReadStr(r),
                incompatibleTraitRefs = ReadStrArray(r),
                modifiers             = ReadArray(r, ReadModifier),
                functionTagRef        = ReadStr(r),
                compatibilities       = ReadArray(r, br => new TraitCompatibilityDto { otherTraitRef = ReadStr(br), opinionDelta = br.ReadSingle() }),
                genetic               = r.ReadBoolean(),
                inheritChance         = r.ReadSingle(),
                birthChance           = r.ReadSingle(),
                aiWeights             = ReadArray(r, br => new TraitAiWeightDto { axisRef = ReadStr(br), weight = br.ReadSingle() }),
                eligibilityJson       = ReadStr(r),
            };
            if (version >= 2)
            {
                dto.templateRef = ReadStr(r);
                dto.values      = ReadEntries(r);
            }
            return dto;
        }

        private static void WriteCharacterTemplate(BinaryWriter w, CharacterTemplateDto t)
        {
            WriteStr(w, t.name);
            WriteFloatArray(w, t.color);
            WriteArray(w, t.attributes, WriteDefinition);
            WriteStr(w, t.raceRef);
            WriteStrArray(w, t.guaranteedTraitRefs);
            WriteStrArray(w, t.randomTraitPoolRefs);
            w.Write(t.attributePointBudget);
            w.Write(t.minAgeDays);
            w.Write(t.maxAgeDays);
        }

        private static CharacterTemplateDto ReadCharacterTemplate(BinaryReader r)
        {
            return new CharacterTemplateDto
            {
                name                 = ReadStr(r),
                color                = ReadFloatArray(r),
                attributes           = ReadArray(r, ReadDefinition),
                raceRef              = ReadStr(r),
                guaranteedTraitRefs  = ReadStrArray(r),
                randomTraitPoolRefs  = ReadStrArray(r),
                attributePointBudget = r.ReadInt32(),
                minAgeDays           = r.ReadInt32(),
                maxAgeDays           = r.ReadInt32(),
            };
        }

        private static void WriteCharacter(BinaryWriter w, CharacterDefinitionDto c)
        {
            WriteStr(w, c.id);
            WriteStr(w, c.templateRef);
            WriteEntries(w, c.values);
            WriteArray(w, c.coreAttributes, (bw, cv) => { WriteStr(bw, cv.attrId); bw.Write(cv.baseValue); });
            WriteArray(w, c.traits, (bw, ti) =>
            {
                WriteStr(bw, ti.traitRef);
                bw.Write(ti.remainingDays);
                bw.Write(ti.stacks);
                WriteStr(bw, ti.sourceTag);
            });
            WriteStr(w, c.fatherRef);
            WriteStr(w, c.motherRef);
            WriteStrArray(w, c.childRefs);
        }

        private static CharacterDefinitionDto ReadCharacter(BinaryReader r)
        {
            return new CharacterDefinitionDto
            {
                id             = ReadStr(r),
                templateRef    = ReadStr(r),
                values         = ReadEntries(r),
                coreAttributes = ReadArray(r, br => new CoreAttributeValueDto { attrId = ReadStr(br), baseValue = br.ReadSingle() }),
                traits         = ReadArray(r, br => new CharacterTraitInstanceDto
                {
                    traitRef      = ReadStr(br),
                    remainingDays = br.ReadSingle(),
                    stacks        = br.ReadInt32(),
                    sourceTag     = ReadStr(br),
                }),
                fatherRef      = ReadStr(r),
                motherRef      = ReadStr(r),
                childRefs      = ReadStrArray(r),
            };
        }

        // ── v2 追加块：属性模板 / 特质模板 / 数字格式（分组标签走 ToolkitBinaryCodec.WriteGroupTag/ReadGroupTag）──

        private static void WriteCoreAttributeTemplate(BinaryWriter w, CoreAttributeTemplateDto t)
        {
            WriteStr(w, t.name);
            WriteFloatArray(w, t.color);
            WriteArray(w, t.attributes, WriteDefinition);
            WriteStr(w, t.categoryEnumRef);
            w.Write(t.minValue);
            w.Write(t.maxValue);
            w.Write(t.defaultBase);
        }

        private static CoreAttributeTemplateDto ReadCoreAttributeTemplate(BinaryReader r)
        {
            return new CoreAttributeTemplateDto
            {
                name            = ReadStr(r),
                color           = ReadFloatArray(r),
                attributes      = ReadArray(r, ReadDefinition),
                categoryEnumRef = ReadStr(r),
                minValue        = r.ReadSingle(),
                maxValue        = r.ReadSingle(),
                defaultBase     = r.ReadSingle(),
            };
        }

        private static void WriteTraitTemplate(BinaryWriter w, TraitTemplateDto t)
        {
            WriteStr(w, t.name);
            WriteFloatArray(w, t.color);
            WriteArray(w, t.attributes, WriteDefinition);
            WriteStr(w, t.categoryEnumRef);
            w.Write(t.lifetime);
            w.Write(t.defaultDurationDays);
        }

        private static TraitTemplateDto ReadTraitTemplate(BinaryReader r)
        {
            return new TraitTemplateDto
            {
                name                = ReadStr(r),
                color               = ReadFloatArray(r),
                attributes          = ReadArray(r, ReadDefinition),
                categoryEnumRef     = ReadStr(r),
                lifetime            = r.ReadInt32(),
                defaultDurationDays = r.ReadSingle(),
            };
        }

        private static void WriteNumberFormatConfig(BinaryWriter w, NumberFormatConfigDto c)
        {
            WriteStr(w, c.name);
            WriteArray(w, c.locales, (bw, loc) =>
            {
                WriteStr(bw, loc.languageCode);
                WriteArray(bw, loc.rules, (bw2, rule) =>
                {
                    bw2.Write(rule.threshold);
                    bw2.Write(rule.divisor);
                    WriteValue(bw2, rule.suffixText);
                    bw2.Write(rule.decimalPlaces);
                });
            });
        }

        private static NumberFormatConfigDto ReadNumberFormatConfig(BinaryReader r)
        {
            return new NumberFormatConfigDto
            {
                name = ReadStr(r),
                locales = ReadArray(r, br => new NumberFormatLocaleDto
                {
                    languageCode = ReadStr(br),
                    rules = ReadArray(br, br2 => new NumberFormatRuleDto
                    {
                        threshold     = br2.ReadInt64(),
                        divisor       = br2.ReadDouble(),
                        suffixText    = ReadValue(br2),
                        decimalPlaces = br2.ReadInt32(),
                    })
                })
            };
        }

        #endregion
    }
}
