using System;

namespace EcaSystems.Core2
{
    public abstract class AEcaCondition<R> : IEcaCondition<R> where R : IEcaRuleState
    {
        private IEcaStateResolver _state;
        protected IEcaStateResolver State => _state
            ?? throw new InvalidOperationException("Condition has not been initialized with StateResolver.");

        internal void Initialize(IEcaStateResolver stateResolver)
        {
            if (stateResolver == null) throw new ArgumentNullException(nameof(stateResolver));
            if (_state != null) throw new InvalidOperationException("Condition is already initialized.");
            _state = stateResolver;
        }

        public abstract string Id { get; }
        public virtual string Name => Id;
        public virtual string Description => null;
        public abstract bool Check(R state, IEcaConditionContext context);
    }
}
