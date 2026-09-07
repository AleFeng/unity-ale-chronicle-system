using System.Collections.Generic;
using Ale.Effect;

namespace Ale.Chronicle
{
    /// <summary>增加职业经验（可为负；角色须在运行时层从事该职业，升级 / 解锁规则照旧）。</summary>
    [EffectExecutor("Chronicle.AddProfessionExp")]
    public sealed class AddProfessionExpExecutor : ChronicleExecutorBase
    {
        private static readonly EffectParamDef[] Schema =
        {
            new EffectParamDef("professionId", EffectParamType.String, false, "职业ID"),
            new EffectParamDef("amount",       EffectParamType.Int,    false, "经验值"),
        };

        public override string Key => "Chronicle.AddProfessionExp";
        public override string DisplayName => "增加职业经验";
        public override IReadOnlyList<EffectParamDef> ParamSchema => Schema;

        protected override EffectResult Execute(IReadOnlyList<EffectParam> parameters, IEffectContext ctx,
            string characterId, IChronicleRuntimeSink sink)
        {
            string professionId = Str(parameters, "professionId");
            if (string.IsNullOrEmpty(professionId)) return EffectResult.Failed("professionId 为空");
            int amount = (int)Int(parameters, "amount");
            return sink.AddProfessionExp(characterId, professionId, amount)
                ? EffectResult.Applied
                : EffectResult.Skipped($"未增加经验（未从事职业 '{professionId}' 或经验为 0）");
        }
    }
}
