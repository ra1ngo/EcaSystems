namespace EcaSystems.Core2
{
    public interface IEcaRuleState
    {
        string RuleId { get; }
    }

    public interface IEcaRuleState<out E> : IEcaRuleState
    {
        E EventState { get; }
    }
}
