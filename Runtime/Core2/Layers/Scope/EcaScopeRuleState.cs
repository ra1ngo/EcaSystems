using System;

namespace EcaSystems.Core2
{
    public sealed class EcaScopeRuleState<E> : IEcaScopeRuleState<E>
    {
        public string RuleId { get; }
        public E EventState { get; }
        public EcaExecutionGroupState ExecutionGroupState { get; }
        public EcaScopeState ScopeState { get; }

        public EcaScopeRuleState(string ruleId, E eventState, EcaExecutionGroupState executionGroupState, EcaScopeState scopeState)
        {
            if (string.IsNullOrWhiteSpace(ruleId)) throw new ArgumentException("Rule id cannot be empty.", nameof(ruleId));
            RuleId = ruleId;
            EventState = eventState;
            ExecutionGroupState = executionGroupState ?? throw new ArgumentNullException(nameof(executionGroupState));
            ScopeState = scopeState ?? throw new ArgumentNullException(nameof(scopeState));
        }
    }
}
