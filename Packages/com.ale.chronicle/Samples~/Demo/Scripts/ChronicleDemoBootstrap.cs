using Ale.Effect;
using UnityEngine;

namespace Ale.Chronicle.Demo
{
    /// <summary>
    /// 演示引导（仅用于 Demo）：Awake 时把指定的编年史数据库注册进 <see cref="ChronicleDataManager"/>（技能 UI 目录来源），
    /// 并把 toolkit 效果库注册进 <see cref="EffectDataManager"/>（技能 / 道具引用的效果按 id 经全局效果注册表解析；0.5.0 起效果不再存于编年史库）。
    ///
    /// <para>正式游戏的数据库注册由存档 / 游戏层驱动，不使用本组件。Awake 早于 Start，故在 UiwSkillView 于 Start 打开采集之前，
    /// 数据库已就绪。放在 Resources 下的效果库也会随启动自动注册（<c>EffectRuntime.AutoLoadFromResources</c>），届时无需本组件登记。</para>
    /// </summary>
    public class ChronicleDemoBootstrap : MonoBehaviour
    {
        [Tooltip("要注册进 ChronicleDataManager 的数据库（技能 UI 目录来源）。")]
        public ChronicleDatabase database;

        [Tooltip("要注册进 EffectDataManager 的 toolkit 效果库（技能 onUseEffectRefs 引用的效果定义 / Gameplay 标签）。")]
        public EffectDatabase effectDatabase;

        private void Awake()
        {
            if (database != null)
                ChronicleDataManager.Instance.Register(database);
            else
                Debug.LogWarning("[ChronicleDemoBootstrap] 未配置 database，技能 UI 目录来源将为空。");

            if (effectDatabase != null)
                EffectDataManager.Instance.Register(effectDatabase);
            else
                Debug.LogWarning("[ChronicleDemoBootstrap] 未配置 effectDatabase，技能使用时的效果将无法按 id 解析。");
        }
    }
}
