namespace EcaSystems.Core2
{
    public interface IEcaScopeRuleState<out E> : IEcaExecutionRuleState<E>
    {
        EcaScopeState ScopeState { get; }
    }
}
