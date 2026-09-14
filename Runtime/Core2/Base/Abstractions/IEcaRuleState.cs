namespace EcaSystems.Core2
{
    public interface IEcaRuleState { }

    public interface IEcaRuleState<out TEventState> : IEcaRuleState
    {
        TEventState EventState { get; }
    }
}
