using System;

namespace EcaSystems.Core
{
    public interface IEcaExecutionContext<out TEventContext> : IEcaContext<TEventContext>
    {
        EcaRuleExecutionState RuleExecutionState { get; }
    }

    public sealed class EcaExecutionContext<TEventContext> : EcaContext<TEventContext>, IEcaExecutionContext<TEventContext>
    {
        public EcaRuleExecutionState RuleExecutionState { get; }

        public EcaExecutionContext(TEventContext eventContext, EcaRuleExecutionState ruleExecutionState)
            : base(eventContext)
        {
            RuleExecutionState = ruleExecutionState ?? throw new ArgumentNullException(nameof(ruleExecutionState));
        }
    }
}