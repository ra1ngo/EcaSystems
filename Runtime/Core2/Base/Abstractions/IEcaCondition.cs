namespace EcaSystems.Core2
{
    public interface IEcaCondition
    {
        string Id { get; }
        string Name { get; }
        string Description { get; }
    }

    public interface IEcaCondition<in R, in C> : IEcaCondition
        where R : IEcaRuleState
        where C : IEcaConditionContext
    {
        bool Check(R state, C context);
    }
}
