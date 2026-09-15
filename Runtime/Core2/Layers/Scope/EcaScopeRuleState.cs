using System;

namespace EcaSystems.Core2
{
    public sealed class EcaScopeRuleState<E> : IEcaScopeRuleState<E>
    {
        public E EventState { get; }
        public EcaExecutionGroupState ExecutionGroupState { get; }
        public EcaScopeState ScopeState { get; }

        public EcaScopeRuleState(E eventState, EcaExecutionGroupState executionGroupState, EcaScopeState scopeState)
        {
            EventState = eventState;
            ExecutionGroupState = executionGroupState ?? throw new ArgumentNullException(nameof(executionGroupState));
            ScopeState = scopeState ?? throw new ArgumentNullException(nameof(scopeState));
        }
    }
}
