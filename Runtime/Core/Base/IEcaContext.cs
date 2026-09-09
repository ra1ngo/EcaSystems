namespace EcaSystems.Core
{
    public interface IEcaContext { }

    public interface IEcaContext<out TEventContext> : IEcaContext
    {
        TEventContext EventContext { get; }
    }
}
