using System;

namespace EcaSystems.Core
{
    public interface IEcaExecutionConditionContext<TEventContext> : IEcaConditionContext<TEventContext>
    {
        EcaRuleExecutionGroupState RuleExecutionGroupState { get; }
    }

    public sealed class EcaExecutionConditionContext<TEventContext>
        : EcaConditionContext<TEventContext>, IEcaExecutionConditionContext<TEventContext>
    {
        public EcaRuleExecutionGroupState RuleExecutionGroupState { get; }

        public EcaExecutionConditionContext(TEventContext eventContext,
            EcaRuleExecutionGroupState ruleExecutionGroupState)
            : base(eventContext)
        {
            RuleExecutionGroupState = ruleExecutionGroupState ?? throw new ArgumentNullException(nameof(ruleExecutionGroupState));
        }
    }
}
