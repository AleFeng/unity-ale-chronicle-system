namespace Ale.Chronicle
{
    /// <summary>
    /// 效果执行器 → 编年史运行时的落地门面（经效果上下文以服务形式提供；执行器不直接依赖各单例，便于替换 / 测试）。
    /// 默认实现 <see cref="ChronicleRuntimeSink"/> 转发到 <see cref="TraitRuntimeManager"/> / <see cref="TitleRuntimeManager"/> /
    /// <see cref="ProfessionRuntimeManager"/> / <see cref="SkillRuntimeManager"/>。各方法返回是否产生了改动。
    /// </summary>
    public interface IChronicleRuntimeSink
    {
        /// <summary>授予特质。durationDays：0 按特质定义、&gt;0 指定天数、&lt;0 强制永久。</summary>
        bool GrantTrait(string characterId, string traitId, float durationDays, int stacks, string sourceTag);

        bool RevokeTrait(string characterId, string traitId);

        bool GrantTitle(string characterId, string titleId);

        bool RevokeTitle(string characterId, string titleId);

        /// <summary>增加职业经验（角色须在运行时层从事该职业）。</summary>
        bool AddProfessionExp(string characterId, string professionId, int amount);

        bool LearnSkill(string characterId, string skillId);

        bool ForgetSkill(string characterId, string skillId);
    }
}
