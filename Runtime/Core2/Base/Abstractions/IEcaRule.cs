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

    public interface IEcaRule<E, R, C, A> : IEcaRule
        where R : IEcaRuleState<E>
        where C : IEcaConditionContext
        where A : IEcaActionContext
    {
        new IEcaEvent<E> Event { get; }
        new IEcaCondition<R, C> Condition { get; }
        new IEcaAction<R, A> Action { get; }
    }
}
