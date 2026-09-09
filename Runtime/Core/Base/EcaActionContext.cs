namespace EcaSystems.Core
{
    public class EcaActionContext<TEventContext>
        : EcaContext<TEventContext>, IEcaActionContext<TEventContext>
    {
        public EcaActionContext(TEventContext eventContext) : base(eventContext) { }
    }
}
