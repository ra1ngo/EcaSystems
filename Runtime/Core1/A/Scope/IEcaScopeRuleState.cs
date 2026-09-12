namespace EcaSystems.Core1
{
    public interface IEcaScopeRuleState<out TEventState> : IEcaExecutionRuleState<TEventState>
    {
        EcaScopeState ScopeState { get; }
    }
}
