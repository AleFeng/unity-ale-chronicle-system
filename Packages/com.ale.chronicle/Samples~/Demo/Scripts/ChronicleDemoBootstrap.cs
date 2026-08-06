using UnityEngine;

namespace Ale.Chronicle.Demo
{
    /// <summary>
    /// 演示引导（仅用于 Demo）：Awake 时把指定的编年史数据库注册进 <see cref="ChronicleDataManager"/>，
    /// 使技能 UI（<c>ESkillSource.Database</c> 目录来源）在 Play 时能显示全部技能。
    ///
    /// <para>正式游戏的数据库注册由存档 / 游戏层驱动，不使用本组件。Awake 早于 Start，
    /// 故在 UiwSkillView 于 Start 打开采集之前，数据库已就绪。</para>
    /// </summary>
    public class ChronicleDemoBootstrap : MonoBehaviour
    {
        [Tooltip("要注册进 ChronicleDataManager 的数据库（技能 UI 目录来源）。")]
        public ChronicleDatabase database;

        private void Awake()
        {
            if (database != null)
                ChronicleDataManager.Instance.Register(database);
            else
                Debug.LogWarning("[ChronicleDemoBootstrap] 未配置 database，技能 UI 目录来源将为空。");
        }
    }
}
