namespace EcaSystems.Core
{
    public interface IEcaRuleExecutionGroup
    {
        string RuleId { get; }
        EcaRuleExecutionGroupState State { get; }
    }
}
