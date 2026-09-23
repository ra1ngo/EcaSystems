namespace EcaSystems.Core2
{
    public interface IEcaExecutionRuleState : IEcaRuleState
    {
        EcaExecutionGroupState ExecutionGroupState { get; }
    }

    public interface IEcaExecutionRuleState<out E> : IEcaExecutionRuleState, IEcaRuleState<E>
    {
    }
}
