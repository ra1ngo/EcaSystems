namespace EcaSystems.Core
{
    public interface IEcaConditionContext : IEcaContext { }

    public interface IEcaConditionContext<out TEventContext>
        : IEcaConditionContext, IEcaContext<TEventContext> { }
}
