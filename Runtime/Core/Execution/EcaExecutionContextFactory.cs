namespace EcaSystems.Core
{
    public sealed class EcaExecutionContextFactory : IEcaExecutionContextFactory
    {
        public EcaExecutionContext<TEventContext> Create<TEventContext>(
            TEventContext eventContext, EcaRuleExecutionGroupState groupState)
        {
            return new EcaExecutionContext<TEventContext>(eventContext, groupState);
        }
    }
}
