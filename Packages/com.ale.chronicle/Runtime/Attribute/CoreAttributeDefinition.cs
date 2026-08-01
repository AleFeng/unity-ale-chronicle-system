using System;
using System.Collections.Generic;
using Ale.Toolkit.Runtime;

namespace Ale.Chronicle
{
    /// <summary>
    /// 核心属性的定义单元（力量/敏捷/智力/魅力/体力/统帅…）：各子系统「加成的靶子」，也是三列编辑器里的<b>实例</b>
    /// （来源模板 <see cref="CoreAttributeTemplate"/>）。值 = 「基础值 + 修饰器栈」，当前值由 <see cref="CoreAttributeResolver"/> 现场求出。
    /// <para>面向玩家的文本 / 资源字段用 <see cref="AttributeValue"/>（Text 走本地化、Sprite 走 Addressable 统一收集）；
    /// <see cref="id"/> 与 <see cref="categoryEnumRef"/> 是稳定键，保持裸 <see cref="string"/>。
    /// 继承 <see cref="AttributeOwner"/> 承载来自模板 schema 的自定义字段 <see cref="values"/>。</para>
    /// </summary>
    [Serializable]
    public class CoreAttributeDefinition : AttributeOwner
    {
        /// <summary>稳定引用键（如 "str"/"力量"；modifier.targetAttributeId 指向它）。</summary>
        public string id;

        /// <summary>来源属性模板名（决定自定义字段 schema 与默认值）。</summary>
        public string templateRef;

        /// <summary>显示名（Text：纯文本 fallback + 可选本地化）。</summary>
        public AttributeValue displayName = new AttributeValue(EFieldType.Text);

        /// <summary>缩写（Text，如 "力"）。</summary>
        public AttributeValue abbreviation = new AttributeValue(EFieldType.Text);

        /// <summary>属性类别（EnumType 名，可选：体格/心智/社交/军事…）。</summary>
        public string categoryEnumRef;

        /// <summary>下限。</summary>
        public float minValue = 0f;

        /// <summary>上限。</summary>
        public float maxValue = 100f;

        /// <summary>新角色默认基础值。</summary>
        public float defaultBase = 0f;

        /// <summary>图标（Sprite：经 Addressable 统一收集）。</summary>
        public AttributeValue icon = new AttributeValue(EFieldType.Sprite);

        /// <summary>说明（Text）。</summary>
        public AttributeValue description = new AttributeValue(EFieldType.Text);

        /// <summary>来自模板 schema 的自定义字段值。</summary>
        public List<AttributeEntry> values = new List<AttributeEntry>();

        // 实现 AttributeOwner：把 values 暴露给基类懒加载字典缓存。
        protected override List<AttributeEntry> AttributeEntries => values;

        public CoreAttributeDefinition() { }

        public CoreAttributeDefinition(string id) { this.id = id; }

        /// <summary>依模板 schema 对账自定义字段（增补缺失 / 移除多余 / 类型漂移重置）。</summary>
        public void RebuildAttributes(IChronicleSchemaSource src)
        {
            if (src == null) return;
            var template = src.GetCoreAttributeTemplate(templateRef);
            AttributeSync.Sync(values, template != null ? template.attributes : null);
            InvalidateEntryCache();
        }

        /// <summary>显示名解析（本地化优先 → 纯文本，均空回退 <see cref="id"/>）。运行时 UI 用。</summary>
        public string ResolveDisplayName()
        {
            string s = displayName != null ? displayName.ResolveText() : null;
            return !string.IsNullOrEmpty(s) ? s : id;
        }

        /// <summary>纯文本显示名（编辑期稳定，均空回退 <see cref="id"/>）。编辑器列表 / 下拉用。</summary>
        public string PlainName()
        {
            string s = displayName != null ? displayName.GetTextValue() : null;
            return !string.IsNullOrEmpty(s) ? s : id;
        }

        /// <summary>归一：确保 Text / Sprite 字段类型正确（修旧数据 / 防漂移；编辑器绘制前调用）。</summary>
        public void Normalize()
        {
            displayName  = EnsureType(displayName,  EFieldType.Text);
            abbreviation = EnsureType(abbreviation, EFieldType.Text);
            description  = EnsureType(description,   EFieldType.Text);
            icon         = EnsureType(icon,          EFieldType.Sprite);
            if (values == null) values = new List<AttributeEntry>();
        }

        private static AttributeValue EnsureType(AttributeValue v, EFieldType t)
        {
            if (v == null) return new AttributeValue(t);
            if (v.Type != t || v.IsArray) v.ChangeType(t, false);
            return v;
        }

        public CoreAttributeDefinition Clone()
        {
            var clone = new CoreAttributeDefinition
            {
                id              = id,
                templateRef     = templateRef,
                displayName     = displayName  != null ? displayName.Clone()  : new AttributeValue(EFieldType.Text),
                abbreviation    = abbreviation != null ? abbreviation.Clone() : new AttributeValue(EFieldType.Text),
                categoryEnumRef = categoryEnumRef,
                minValue        = minValue,
                maxValue        = maxValue,
                defaultBase     = defaultBase,
                icon            = icon        != null ? icon.Clone()        : new AttributeValue(EFieldType.Sprite),
                description     = description != null ? description.Clone() : new AttributeValue(EFieldType.Text),
            };
            foreach (var e in values)
                clone.values.Add(e != null ? e.Clone() : new AttributeEntry());
            return clone;
        }
    }
}
