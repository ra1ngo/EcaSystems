namespace EcaSystems.Core2
{
    public abstract class AEcaCondition<R> : IEcaCondition<R> where R : IEcaRuleState
    {
        public abstract string Id { get; }
        public virtual string Name => Id;
        public virtual string Description => null;
        public abstract bool Check(R state, IEcaConditionContext context);
    }
}
