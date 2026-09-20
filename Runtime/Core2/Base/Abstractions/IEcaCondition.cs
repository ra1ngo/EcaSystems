namespace EcaSystems.Core2
{
    public interface IEcaCondition
    {
        string Id { get; }
        string Name { get; }
        string Description { get; }
    }

    public interface IEcaCondition<in R> : IEcaCondition
        where R : IEcaRuleState
    {
        bool Check(R state, IEcaConditionContext context);
    }
}
