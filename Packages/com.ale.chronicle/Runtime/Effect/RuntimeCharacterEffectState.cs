using System;
using System.Collections.Generic;
using Ale.Effect;
using Ale.Modifier;

namespace Ale.Chronicle
{
    /// <summary>
    /// 某角色的运行时效果存档状态：效果容器状态（活动效果 / 句柄 / 松散标签，来自 toolkit <see cref="EffectContainer.ExportState"/>）
    /// + 永久落地的修饰器（瞬时 / 周期效果经 <c>IEffectAttributeSink</c> 写入，来源 <c>effect:{id}#{句柄}</c>，不改配置基础值、可追溯）。
    /// </summary>
    [Serializable]
    public class RuntimeCharacterEffectState
    {
        public string characterId;

        /// <summary>效果容器状态。</summary>
        public EffectContainerState container = new EffectContainerState();

        /// <summary>永久落地的修饰器（按落地顺序）。</summary>
        public List<ModifierDefinition> permanentModifiers = new List<ModifierDefinition>();

        public RuntimeCharacterEffectState() { }

        public RuntimeCharacterEffectState(string characterId)
        {
            this.characterId = characterId;
        }

        public RuntimeCharacterEffectState Clone()
        {
            var clone = new RuntimeCharacterEffectState(characterId) { container = CloneContainerState(container) };
            foreach (var m in permanentModifiers)
                clone.permanentModifiers.Add(m != null ? m.Clone() : new ModifierDefinition());
            return clone;
        }

        /// <summary>深拷贝容器状态（toolkit 状态类为纯数据，无 Clone）。</summary>
        public static EffectContainerState CloneContainerState(EffectContainerState src)
        {
            var dst = new EffectContainerState();
            if (src == null) return dst;
            dst.nextHandle = src.nextHandle;
            if (src.looseTags != null)      dst.looseTags.AddRange(src.looseTags);
            if (src.looseTagCounts != null) dst.looseTagCounts.AddRange(src.looseTagCounts);
            if (src.effects != null)
                foreach (var e in src.effects)
                {
                    if (e == null) continue;
                    var c = new ActiveEffectState
                    {
                        effectId    = e.effectId,
                        handle      = e.handle,
                        level       = e.level,
                        stacks      = e.stacks,
                        duration    = e.duration,
                        remaining   = e.remaining,
                        period      = e.period,
                        periodTimer = e.periodTimer,
                        elapsed     = e.elapsed,
                        sourceTag   = e.sourceTag,
                    };
                    if (e.setByCallerKeys   != null) c.setByCallerKeys.AddRange(e.setByCallerKeys);
                    if (e.setByCallerValues != null) c.setByCallerValues.AddRange(e.setByCallerValues);
                    if (e.baseMagnitudes    != null) c.baseMagnitudes.AddRange(e.baseMagnitudes);
                    dst.effects.Add(c);
                }
            return dst;
        }
    }
}
