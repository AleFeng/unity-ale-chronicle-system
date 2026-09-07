using System;
using Ale.Effect;
using Ale.Toolkit.Runtime;

namespace Ale.Chronicle
{
    /// <summary>
    /// 效果条目（编年史包装实体）：toolkit 的 <see cref="EffectDefinition"/>（GAS GameplayEffect：时长 / 周期 / 叠加 / 标签 / 修饰器 / 执行）
    /// 外加面向玩家的本地化显示字段（名称 / 描述 / 图标，走 <see cref="AttributeValue"/>）。技能「使用」、道具「使用」等以 <see cref="id"/> 引用。
    ///
    /// <para><b>放置</b>：位于 <see cref="ChronicleDatabase.Effects"/> 顶层列表——定义内嵌的 执行表达式 → 组 → 项 → 条件 → 组 → 项 → 参数 已达 8 层，
    /// 本包装再占 1 层，不可再多包。<see cref="Normalize"/> 把 <see cref="id"/> 与纯文本名同步进 <see cref="definition"/>（定义自身也有 id / displayName，
    /// 供 toolkit 侧与执行信息使用），并对定义做归一（补 null、空阶段改写为 onApply）。</para>
    /// </summary>
    [Serializable]
    public class ChronicleEffect
    {
        /// <summary>稳定引用键（与 <see cref="definition"/>.id 同步）。</summary>
        public string id;

        /// <summary>显示名（Text：纯文本 fallback + 可选本地化引用；为空时回退 <see cref="id"/>）。</summary>
        public AttributeValue displayText = new AttributeValue(EFieldType.Text);

        /// <summary>描述（Text）。</summary>
        public AttributeValue descriptionText = new AttributeValue(EFieldType.Text);

        /// <summary>图标（Sprite 对象类属性值：直接引用或 Addressable 授权 GUID）。</summary>
        public AttributeValue iconValue = new AttributeValue(EFieldType.Sprite);

        /// <summary>效果定义（toolkit GAS 层）。</summary>
        public EffectDefinition definition = new EffectDefinition();

        public ChronicleEffect()
        {
        }

        public ChronicleEffect(string id, EDurationPolicy durationPolicy = EDurationPolicy.Instant)
        {
            this.id    = id;
            definition = new EffectDefinition(id, durationPolicy);
        }

        /// <summary>显示名解析（本地化优先 → 纯文本，均空回退 <see cref="id"/>）。运行时 UI 用。</summary>
        public string ResolveDisplayName()
        {
            string s = displayText != null ? displayText.ResolveText() : null;
            return !string.IsNullOrEmpty(s) ? s : id;
        }

        /// <summary>纯文本显示名（编辑期稳定，均空回退 <see cref="id"/>）。编辑器列表 / 下拉用。</summary>
        public string PlainName()
        {
            string s = displayText != null ? displayText.GetTextValue() : null;
            return !string.IsNullOrEmpty(s) ? s : id;
        }

        /// <summary>归一（幂等）：确保 Text / Sprite 字段类型正确、定义非空、定义 id / displayName 与本条目同步，并对定义 <see cref="EffectDefinition.Normalize"/>。</summary>
        public void Normalize()
        {
            displayText     = EnsureType(displayText,     EFieldType.Text);
            descriptionText = EnsureType(descriptionText, EFieldType.Text);
            iconValue       = EnsureType(iconValue,       EFieldType.Sprite);
            definition ??= new EffectDefinition();
            definition.id          = id;
            definition.displayName = PlainName();
            definition.Normalize();
        }

        private static AttributeValue EnsureType(AttributeValue v, EFieldType t)
        {
            if (v == null) return new AttributeValue(t);
            if (v.Type != t || v.IsArray) v.ChangeType(t, false);
            return v;
        }

        /// <summary>深拷贝。</summary>
        public ChronicleEffect Clone()
        {
            return new ChronicleEffect
            {
                id              = id,
                displayText     = displayText     != null ? displayText.Clone()     : new AttributeValue(EFieldType.Text),
                descriptionText = descriptionText != null ? descriptionText.Clone() : new AttributeValue(EFieldType.Text),
                iconValue       = iconValue       != null ? iconValue.Clone()       : new AttributeValue(EFieldType.Sprite),
                definition      = definition      != null ? definition.Clone()      : new EffectDefinition(),
            };
        }
    }
}
