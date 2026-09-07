namespace Ale.Chronicle
{
    /// <summary><see cref="IChronicleRuntimeSink"/> 默认实现：转发到各运行时管理器单例。</summary>
    public sealed class ChronicleRuntimeSink : IChronicleRuntimeSink
    {
        public static readonly ChronicleRuntimeSink Instance = new ChronicleRuntimeSink();

        public bool GrantTrait(string characterId, string traitId, float durationDays, int stacks, string sourceTag)
            => TraitRuntimeManager.Instance.Grant(characterId, traitId, durationDays, stacks, sourceTag);

        public bool RevokeTrait(string characterId, string traitId)
            => TraitRuntimeManager.Instance.Revoke(characterId, traitId);

        public bool GrantTitle(string characterId, string titleId)
        {
            var tm = TitleRuntimeManager.Instance;
            if (tm.Has(characterId, titleId)) return false;
            tm.Grant(characterId, titleId, ChronicleClock.Instance.WorldDay);
            return tm.Has(characterId, titleId);
        }

        public bool RevokeTitle(string characterId, string titleId)
            => TitleRuntimeManager.Instance.Revoke(characterId, titleId);

        public bool AddProfessionExp(string characterId, string professionId, int amount)
        {
            var pm = ProfessionRuntimeManager.Instance;
            if (amount == 0 || !pm.HasProfession(characterId, professionId)) return false;
            pm.AddExp(characterId, professionId, amount);
            return true;
        }

        public bool LearnSkill(string characterId, string skillId)
            => SkillRuntimeManager.Instance.Learn(characterId, skillId);

        public bool ForgetSkill(string characterId, string skillId)
            => SkillRuntimeManager.Instance.Forget(characterId, skillId);
    }
}
