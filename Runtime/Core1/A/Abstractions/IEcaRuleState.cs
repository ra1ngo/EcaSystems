namespace EcaSystems.Core1
{
    public interface IEcaRuleState { }

    public interface IEcaRuleState<out TEventState> : IEcaRuleState
    {
        TEventState EventState { get; }
    }
}
