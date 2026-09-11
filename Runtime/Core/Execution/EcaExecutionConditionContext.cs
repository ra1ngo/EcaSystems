using System;

namespace EcaSystems.Core
{
    public interface IEcaExecutionConditionContext<TEventContext>
        : IEcaExecutionContext<TEventContext>, IEcaConditionContext<TEventContext>
    {
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
