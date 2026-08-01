namespace Ale.Chronicle
{
    /// <summary>特质时效：永久（伴随角色一生，除非被主动移除）或临时（有倒计时，到期自动移除）。</summary>
    public enum ETraitLifetime
    {
        /// <summary>永久：不设倒计时。</summary>
        Permanent = 0,

        /// <summary>临时：按 <see cref="TraitDefinition.defaultDurationDays"/> 倒计时，到期移除。</summary>
        Temporary = 1,
    }
}
