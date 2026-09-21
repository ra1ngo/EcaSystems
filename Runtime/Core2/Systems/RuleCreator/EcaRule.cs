namespace EcaSystems.Core2
{
    public sealed class EcaRule<E, R> : IEcaRule<E, R> where R : IEcaRuleState<E>
    {
        public string Id { get; }
        public string Name => Id;
        public string Description => null;
        public IEcaEvent<E> Event { get; }
        public IEcaCondition<R> Condition { get; }
        public IEcaAction<R> Action { get; }
        IEcaEvent IEcaRule.Event => Event;
        IEcaCondition IEcaRule.Condition => Condition;
        IEcaAction IEcaRule.Action => Action;

        internal EcaRule(string id, IEcaEvent<E> ecaEvent, IEcaCondition<R> condition, IEcaAction<R> action)
        {
            Id = id;
            Event = ecaEvent;
            Condition = condition;
            Action = action;
        }
    }
}
