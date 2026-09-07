using System;
using Ale.Effect;
using Ale.Toolkit.Runtime;

namespace Ale.Chronicle
{
    /// <summary>
    /// <b>0.4.0 legacy</b> 效果条目（编年史包装实体）：toolkit 的 <see cref="EffectDefinition"/> 外加本地化显示字段（名称 / 描述 / 图标）。
    /// 0.5.0 起效果外移至 toolkit 效果库（<c>EffectDatabase</c> / <c>EffectEntry</c>，所有上层系统共用），本类型只用于承接旧资产 /
    /// 旧二进制里的数据（<c>ChronicleDatabase.LegacyEffects</c>）并经 <see cref="ChronicleLegacyEffects"/> / 迁移菜单迁入效果库。
    /// </summary>
    [Obsolete("0.5.0：效果已外移至 toolkit 效果库（EffectDatabase / EffectEntry）；本类型仅用于 legacy 字段反序列化与 ChronicleLegacyEffects 迁移。")]
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
