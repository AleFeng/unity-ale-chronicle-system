using System.Collections.Generic;
using Ale.Effect;

namespace Ale.Chronicle
{
    /// <summary>
    /// 编年史效果执行器基类：目标角色 id 取自上下文主体（<see cref="IEffectContext.Subject"/>，由 <see cref="ChronicleEffectContext"/> /
    /// toolkit 执行上下文置为效果目标），落地经上下文服务 <see cref="IChronicleRuntimeSink"/>（缺失 → 执行失败）。
    /// 键统一 <c>Chronicle.*</c>、类别「角色」，与条件判定器写法一致；属性增减不做执行器，由效果定义的 modifiers 承担。
    /// </summary>
    public abstract class ChronicleExecutorBase : IEffectExecutor
    {
        public abstract string Key { get; }
        public abstract string DisplayName { get; }
        public string Category => "角色";
        public abstract IReadOnlyList<EffectParamDef> ParamSchema { get; }

        public EffectResult Execute(IReadOnlyList<EffectParam> parameters, IEffectContext ctx)
        {
            var characterId = ctx?.Subject as string;
            if (string.IsNullOrEmpty(characterId)) return EffectResult.Failed("上下文主体不是角色 id");
            var sink = ctx.GetService<IChronicleRuntimeSink>();
            if (sink == null) return EffectResult.Failed("上下文缺少 IChronicleRuntimeSink");
            return Execute(parameters, ctx, characterId, sink);
        }

        protected abstract EffectResult Execute(IReadOnlyList<EffectParam> parameters, IEffectContext ctx,
            string characterId, IChronicleRuntimeSink sink);

        // ── 参数助手 ────────────────────────────────────────────────────────────────

        protected static EffectParam Find(IReadOnlyList<EffectParam> parameters, string id)
        {
            if (parameters == null) return null;
            for (int i = 0; i < parameters.Count; i++)
                if (parameters[i] != null && parameters[i].id == id) return parameters[i];
            return null;
        }

        protected static string Str(IReadOnlyList<EffectParam> parameters, string id) => Find(parameters, id)?.GetString();
        protected static long   Int(IReadOnlyList<EffectParam> parameters, string id) => Find(parameters, id)?.GetInt() ?? 0L;
        protected static double Float(IReadOnlyList<EffectParam> parameters, string id) => Find(parameters, id)?.GetFloat() ?? 0d;

        /// <summary>当前效果的来源标记：活动实例的 sourceTag；瞬时效果回退定义默认 <c>effect:{id}</c>；无执行信息 → null。</summary>
        protected static string SourceTagOf(IEffectContext ctx)
        {
            var info = ctx?.GetService<IEffectExecutionInfo>();
            if (info == null) return null;
            return info.Effect?.SourceTag ?? info.Definition?.DefaultSourceTag;
        }
    }
}
