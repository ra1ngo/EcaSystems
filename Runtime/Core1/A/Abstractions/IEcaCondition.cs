namespace EcaSystems.Core1
{
    public interface IEcaCondition
    {
        string Id { get; }
        string Name { get; }
        string Description { get; }
    }

    public interface IEcaCondition<in TRuleState, in TRunnerContext> : IEcaCondition
        where TRuleState : IEcaRuleState
        where TRunnerContext : IEcaConditionRunnerContext
    {
        bool Check(TRuleState state, TRunnerContext runnerContext);
    }
}
