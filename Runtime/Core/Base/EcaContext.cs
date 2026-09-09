namespace EcaSystems.Core
{
    public class EcaContext<TEventContext> : IEcaContext<TEventContext>
    {
        public TEventContext EventContext { get; }

        public EcaContext(TEventContext eventContext)
        {
            EventContext = eventContext;
        }
    }
}
