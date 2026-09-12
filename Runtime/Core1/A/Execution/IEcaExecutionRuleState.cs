namespace EcaSystems.Core1
{
    public interface IEcaExecutionRuleState<out TEventState> : IEcaRuleState<TEventState>
    {
        EcaRuleExecutionGroupState RuleExecutionGroupState { get; }
    }
}
