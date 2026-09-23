namespace EcaSystems.Core2
{
    public interface IEcaScopeRuleState : IEcaExecutionRuleState
    {
        EcaScopeState ScopeState { get; }
    }

    public interface IEcaScopeRuleState<out E> : IEcaScopeRuleState, IEcaExecutionRuleState<E>
    {
    }
}
