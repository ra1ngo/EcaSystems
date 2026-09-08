namespace EcaSystems.Core
{
    public interface IEcaExecutionContextFactory
    {
        EcaExecutionContext<TEventContext> Create<TEventContext>(
            TEventContext eventContext, EcaRuleExecutionGroupState groupState);
    }
}
