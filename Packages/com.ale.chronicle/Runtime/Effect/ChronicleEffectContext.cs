using Ale.Effect;
using Ale.GameplayTags;

namespace Ale.Chronicle
{
    /// <summary>
    /// 组装效果 / 条件上下文（<see cref="EffectContext"/>）：主体 = 目标角色 id，并注册编年史运行时服务——
    /// <see cref="IChronicleConditionSource"/>（主体 = 目标、对象 = 来源）、<see cref="IEffectDefinitionSource"/>（toolkit 效果库 <see cref="EffectDataManager"/>；
    /// 未命中时 <c>EffectApplier</c> 回退全局效果注册表，故其它系统库里的效果同样可按 id 施加）、
    /// <see cref="IEffectContainerSource"/> / <see cref="IEffectAttributeSink"/> / <see cref="IEffectAttributeSource"/> / <see cref="IGameplayTagSource"/>
    /// （效果运行时管理器）、<see cref="IChronicleRuntimeSink"/>（执行器落地门面）、可选 <see cref="IEffectCueSink"/>。
    /// 供 <see cref="EffectRuntimeManager"/> 施加 / 推进、属性条件修改值求值、以及业务层直接调用 toolkit <c>EffectApplier</c> 使用。
    /// </summary>
    public static class ChronicleEffectContext
    {
        public static EffectContext Create(string targetCharacterId, string sourceCharacterId = null, string secondaryCharacterId = null)
        {
            var em  = EffectRuntimeManager.Instance;
            var ctx = new EffectContext { Subject = targetCharacterId };

            ctx.RegisterService<IChronicleConditionSource>(
                new ChronicleRuntimeConditionSource(targetCharacterId, sourceCharacterId, secondaryCharacterId));
            var edm = EffectDataManager.Instance;
            if (edm != null) ctx.RegisterService<IEffectDefinitionSource>(edm);
            ctx.RegisterService<IEffectContainerSource>(em);
            ctx.RegisterService<IEffectAttributeSink>(em);
            ctx.RegisterService<IEffectAttributeSource>(em);
            ctx.RegisterService<IGameplayTagSource>(em);
            ctx.RegisterService<IChronicleRuntimeSink>(em.RuntimeSink ?? ChronicleRuntimeSink.Instance);
            if (em.CueSink != null) ctx.RegisterService<IEffectCueSink>(em.CueSink);
            return ctx;
        }
    }
}
