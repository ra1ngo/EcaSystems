namespace EcaSystems.Core2
{
    public interface IEcaExecutionRuleState<out E> : IEcaRuleState<E>
    {
        EcaExecutionGroupState ExecutionGroupState { get; }
    }
}
