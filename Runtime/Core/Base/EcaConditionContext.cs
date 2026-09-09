namespace EcaSystems.Core
{
    public class EcaConditionContext<TEventContext>
        : EcaContext<TEventContext>, IEcaConditionContext<TEventContext>
    {
        public EcaConditionContext(TEventContext eventContext) : base(eventContext) { }
    }
}
