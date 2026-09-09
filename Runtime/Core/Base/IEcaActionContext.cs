namespace EcaSystems.Core
{
    public interface IEcaActionContext : IEcaContext { }

    public interface IEcaActionContext<out TEventContext>
        : IEcaActionContext, IEcaContext<TEventContext> { }
}
