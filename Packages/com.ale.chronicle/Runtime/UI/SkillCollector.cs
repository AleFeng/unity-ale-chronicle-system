using System.Collections.Generic;

namespace Ale.Chronicle.Runtime.UI
{
    /// <summary>技能信息来源：决定运行时技能 UI 从何处采集要显示的技能集合。</summary>
    public enum ESkillSource
    {
        /// <summary>从已注册数据库获取全部技能（技能书 / 图鉴等目录展示）。</summary>
        Database,

        /// <summary>某角色当前已学会的技能（读取 <see cref="SkillRuntimeManager"/>，按学习顺序）。</summary>
        Character,
    }

    /// <summary>
    /// 技能采集器。按 <see cref="ESkillSource"/> 从两种来源采集要显示的技能集合（保序）。
    /// 无运行时可变状态，故为静态工具类。
    ///
    /// <para>目录来源经 <see cref="ChronicleDataManager.GetAllSkills"/> 取全部已注册技能；
    /// 角色已学来源经 <see cref="SkillRuntimeManager.GetEffectiveSkills"/> 取该角色<b>有效</b>技能
    /// （永久已学 ∪ 装备等外部来源提供）。</para>
    /// </summary>
    public static class SkillCollector
    {
        /// <summary>
        /// 按来源采集技能集合。
        /// </summary>
        /// <param name="source">技能信息来源。</param>
        /// <param name="configId">来源配置：Character = 角色 ID；Database 忽略。</param>
        public static List<Skill> Collect(ESkillSource source, string configId)
        {
            switch (source)
            {
                case ESkillSource.Character: return CollectFromCharacter(configId);
                default:                     return CollectFromDatabase();
            }
        }

        /// <summary>目录来源：返回全部已注册数据库中的技能（跨库 id 去重、保序）。</summary>
        private static List<Skill> CollectFromDatabase()
        {
            var dm = ChronicleDataManager.Instance;
            return dm != null ? dm.GetAllSkills() : new List<Skill>();
        }

        /// <summary>角色来源：读取 <see cref="SkillRuntimeManager"/> 中该角色的<b>有效</b>技能
        /// （永久已学 ∪ 装备等外部来源提供，保序）。</summary>
        private static List<Skill> CollectFromCharacter(string characterId)
        {
            var mgr = SkillRuntimeManager.Instance;
            if (mgr == null || string.IsNullOrEmpty(characterId)) return new List<Skill>();
            return mgr.GetEffectiveSkills(characterId);
        }
    }
}
