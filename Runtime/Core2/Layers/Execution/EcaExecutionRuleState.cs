using System;

namespace EcaSystems.Core2
{
    public sealed class EcaExecutionRuleState<E> : IEcaExecutionRuleState<E>
    {
        public E EventState { get; }
        public EcaExecutionGroupState ExecutionGroupState { get; }

        public EcaExecutionRuleState(E eventState, EcaExecutionGroupState executionGroupState)
        {
            EventState = eventState;
            ExecutionGroupState = executionGroupState ?? throw new ArgumentNullException(nameof(executionGroupState));
        }
    }
}
