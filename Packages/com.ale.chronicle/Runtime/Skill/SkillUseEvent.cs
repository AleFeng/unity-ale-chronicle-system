namespace Ale.Chronicle
{
    /// <summary>
    /// 技能「使用 / 施放」一次性事件负载：对 <see cref="TargetCharacterId"/> 施放 <see cref="SkillId"/>，
    /// 触发来源由 <see cref="SourceKey"/> 标识（可空，如触发本次使用的道具 ID）。
    ///
    /// <para>由 <see cref="SkillRuntimeManager.UseSkill"/> 触发、经 <see cref="SkillRuntimeManager.OnSkillUsed"/> 派发；
    /// 具体效果（治疗 / 加 buff 等）由业务层订阅实现——本阶段 Chronicle 不内置任何效果执行、也不改任何状态。</para>
    /// </summary>
    public readonly struct SkillUseEvent
    {
        /// <summary>被施放技能的目标角色 ID。</summary>
        public readonly string TargetCharacterId;

        /// <summary>被施放的技能 ID。</summary>
        public readonly string SkillId;

        /// <summary>施放来源标识（可空；如触发本次使用的道具 ID）。</summary>
        public readonly string SourceKey;

        public SkillUseEvent(string targetCharacterId, string skillId, string sourceKey)
        {
            TargetCharacterId = targetCharacterId;
            SkillId           = skillId;
            SourceKey         = sourceKey;
        }
    }
}
