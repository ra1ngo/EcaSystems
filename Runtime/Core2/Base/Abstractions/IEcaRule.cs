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

    public interface IEcaRule<E> : IEcaRule
    {
        new IEcaEvent<E> Event { get; }
    }

    public interface IEcaRule<E, R> : IEcaRule<E>
        where R : IEcaRuleState<E>
    {
        new IEcaCondition<R> Condition { get; }
        new IEcaAction<R> Action { get; }
    }
}
