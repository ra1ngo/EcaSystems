namespace EcaSystems.Core2
{
    public interface IEcaCondition
    {
        string Id { get; }
        string Name { get; }
        string Description { get; }
    }

    public interface IEcaCondition<in TRuleState, in TConditionContext> : IEcaCondition
        where TRuleState : IEcaRuleState
        where TConditionContext : IEcaConditionContext
    {
        bool Check(TRuleState state, TConditionContext context);
    }
}
