using System;

namespace EcaSystems.Core2
{
    public sealed class EcaScope : IDisposable, IEcaEventHandler
    {
        private readonly EcaScopeRuntime _owner;
        private EcaExecutionRuntime _executionRuntime;

        public IEcaEventEmitter EventEmitter { get; }
        public EcaScopeState State { get; }
        public string ScopeId => State.ScopeId;
        public string ParentScopeId { get; }
        public bool IsDisposed { get; private set; }

        internal EcaScope(EcaScopeRuntime owner, EcaScopeState state, EcaExecutionRuntime executionRuntime, string parentScopeId, IEcaEventRegistry events)
        {
            _owner = owner;
            ParentScopeId = parentScopeId;
            State = state ?? throw new ArgumentNullException(nameof(state));
            _executionRuntime = executionRuntime ?? throw new ArgumentNullException(nameof(executionRuntime));
            var emitter = new EcaEventEmitter(events, ThrowIfDisposed);
            emitter.Bind(this);
            EventEmitter = emitter;
        }

        public void Register<E, R>(
            IEcaRule<E, R> rule, EcaExecutionMode executionMode,
            Func<IEcaExecutionRuleState<E>, EcaScopeState, R> extendState)
            where R : IEcaScopeRuleState<E>
        {
            ThrowIfDisposed();
            if (extendState == null) throw new ArgumentNullException(nameof(extendState));
            _executionRuntime.Register(rule, executionMode, (eventState, groupState) =>
                extendState(new EcaExecutionRuleState<E>(eventState, groupState), State));
        }

        public void Register<E>(IEcaRule<E, EcaScopeRuleState<E>> rule, EcaExecutionMode executionMode)
        {
            Register(rule, executionMode, (executionState, scopeState) => new EcaScopeRuleState<E>(
                executionState.EventState, executionState.ExecutionGroupState, scopeState));
        }

        public void Fire<E>(IEcaEvent<E> ecaEvent, E eventState, IEcaConditionContext conditionContext = null, IEcaActionContext actionContext = null)
        {
            ThrowIfDisposed();
            _executionRuntime.Fire<E>(ecaEvent, eventState, conditionContext, actionContext);
        }

        void IEcaEventHandler.Handle<E>(IEcaEvent<E> ecaEvent, E eventState,
            IEcaConditionContext conditionContext, IEcaActionContext actionContext) =>
            Fire(ecaEvent, eventState, conditionContext, actionContext);

        public EcaScope CreateScope(string scopeId = null)
        {
            ThrowIfDisposed();
            return _owner.CreateScope(this, scopeId);
        }

        public bool Unregister(IEcaRule rule)
        {
            ThrowIfDisposed();
            return _executionRuntime.Unregister(rule);
        }

        public void Fire<E, R>(
            IEcaEvent<E> ecaEvent, E eventState, Func<IEcaRule<E, R>, E, R> createState,
            IEcaConditionContext conditionContext = null, IEcaActionContext actionContext = null)
            where R : IEcaRuleState<E>
        {
            ThrowIfDisposed();
            _executionRuntime.Fire(ecaEvent, eventState, createState, conditionContext, actionContext);
        }

        public IEcaExecutionGroup GetGroup(string ruleId)
        {
            ThrowIfDisposed();
            return _executionRuntime.GetGroup(ruleId);
        }

        public bool TryGetGroup(string ruleId, out IEcaExecutionGroup group)
        {
            ThrowIfDisposed();
            return _executionRuntime.TryGetGroup(ruleId, out group);
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            _owner.DisposeScope(this);
            // Новые операции закрыты; текущие Fire и Action Tasks удерживают свои Groups до завершения.
            _executionRuntime = null;
        }

        private void ThrowIfDisposed()
        {
            if (IsDisposed) throw new ObjectDisposedException(nameof(EcaScope));
        }
    }
}
