namespace EcaSystems.Core2
{
    public interface IEcaRule
    {
        string Id { get; }
        string Name { get; }
        string Description { get; }
        IEcaEvent Event { get; }
        IEcaCondition Condition { get; }
        IEcaAction Action { get; }
    }

    public interface IEcaRule<TEventState, TRuleState, TConditionContext, TActionContext> : IEcaRule
        where TRuleState : IEcaRuleState<TEventState>
        where TConditionContext : IEcaConditionContext
        where TActionContext : IEcaActionContext
    {
        new IEcaEvent<TEventState> Event { get; }
        new IEcaCondition<TRuleState, TConditionContext> Condition { get; }
        new IEcaAction<TRuleState, TActionContext> Action { get; }
    }
}
