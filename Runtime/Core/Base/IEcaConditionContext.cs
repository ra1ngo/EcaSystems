namespace EcaSystems.Core
{
    public interface IEcaConditionContext : IEcaContext { }

    public interface IEcaConditionContext<TEventContext>
        : IEcaConditionContext, IEcaContext<TEventContext> { }
}
