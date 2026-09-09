namespace EcaSystems.Core
{
    public interface IEcaActionContext : IEcaContext { }

    public interface IEcaActionContext<TEventContext>
        : IEcaActionContext, IEcaContext<TEventContext> { }
}
