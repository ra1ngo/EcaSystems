using System;

namespace EcaSystems.Core
{
    public interface IEcaExecutionContext<out TEventContext> : IEcaContext<TEventContext>
    {
        EcaRuleExecutionGroupState RuleExecutionGroupState { get; }
    }

    public sealed class EcaExecutionContext<TEventContext> : EcaContext<TEventContext>, IEcaExecutionContext<TEventContext>
    {
        // EventContext is the logically immutable payload shared by one Fire; it is not copied.
        public EcaRuleExecutionGroupState RuleExecutionGroupState { get; }

        public EcaExecutionContext(TEventContext eventContext, EcaRuleExecutionGroupState ruleExecutionGroupState)
            : base(eventContext)
        {
            RuleExecutionGroupState = ruleExecutionGroupState ?? throw new ArgumentNullException(nameof(ruleExecutionGroupState));
        }
    }
}
