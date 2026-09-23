using System;

namespace EcaSystems.Core2
{
    public sealed class EcaExecutionRuleState<E> : IEcaExecutionRuleState<E>
    {
        public string RuleId { get; }
        public E EventState { get; }
        public EcaExecutionGroupState ExecutionGroupState { get; }

        public EcaExecutionRuleState(string ruleId, E eventState, EcaExecutionGroupState executionGroupState)
        {
            if (string.IsNullOrWhiteSpace(ruleId)) throw new ArgumentException("Rule id cannot be empty.", nameof(ruleId));
            RuleId = ruleId;
            EventState = eventState;
            ExecutionGroupState = executionGroupState ?? throw new ArgumentNullException(nameof(executionGroupState));
        }
    }
}
