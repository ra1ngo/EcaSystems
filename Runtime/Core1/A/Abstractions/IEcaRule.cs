namespace EcaSystems.Core1
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

    public interface IEcaRule<TEventState, TRuleState, TConditionRunnerContext, TActionRunnerContext> : IEcaRule
        where TRuleState : IEcaRuleState<TEventState>
        where TConditionRunnerContext : IEcaConditionRunnerContext
        where TActionRunnerContext : IEcaActionRunnerContext
    {
        new IEcaEvent<TEventState> Event { get; }
        new IEcaCondition<TRuleState, TConditionRunnerContext> Condition { get; }
        new IEcaAction<TRuleState, TActionRunnerContext> Action { get; }
    }
}
