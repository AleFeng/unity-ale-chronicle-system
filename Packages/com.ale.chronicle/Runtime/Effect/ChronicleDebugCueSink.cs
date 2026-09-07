using Ale.Effect;
using Ale.GameplayTags;
using UnityEngine;

namespace Ale.Chronicle
{
    /// <summary>
    /// 效果线索（Cue）的调试落地：把 施加 / 结算 / 移除 打成 <c>Debug.Log</c>。演示 / 排错用；
    /// 挂到 <see cref="EffectRuntimeManager.CueSink"/> 即生效，正式表现层（图标 / 飘字）自行实现 <see cref="IEffectCueSink"/> 替换。
    /// </summary>
    public sealed class ChronicleDebugCueSink : IEffectCueSink
    {
        public void OnCue(GameplayTag cue, EEffectCueEvent cueEvent, IEffectExecutionInfo info)
        {
            Debug.Log($"[EffectCue] {cue} · {cueEvent} · 效果={info?.Definition?.id} · 目标={info?.Target} · 来源={info?.Source} · L{info?.Level}");
        }
    }
}
