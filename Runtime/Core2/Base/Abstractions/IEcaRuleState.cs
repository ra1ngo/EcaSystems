namespace EcaSystems.Core2
{
    public interface IEcaRuleState { }

    public interface IEcaRuleState<out E> : IEcaRuleState
    {
        E EventState { get; }
    }
}
