using System;

namespace EcaSystems.Core2
{
    public sealed class EcaScope : IDisposable
    {
        private readonly EcaScopeRuntime _owner;
        private EcaExecutionRuntime _executionRuntime;

        public EcaScopeState State { get; }
        public string ScopeId => State.ScopeId;
        public string ParentScopeId { get; }
        public bool IsDisposed { get; private set; }

        internal EcaScope(EcaScopeRuntime owner, EcaScopeState state, EcaExecutionRuntime executionRuntime, string parentScopeId)
        {
            _owner = owner;
            ParentScopeId = parentScopeId;
            State = state ?? throw new ArgumentNullException(nameof(state));
            _executionRuntime = executionRuntime ?? throw new ArgumentNullException(nameof(executionRuntime));
        }

        public void Register<E, R, C, A>(
            IEcaRule<E, R, C, A> rule, EcaExecutionMode executionMode,
            Func<IEcaExecutionRuleState<E>, EcaScopeState, R> extendState)
            where R : IEcaScopeRuleState<E>
            where C : IEcaScopeConditionContext
            where A : IEcaScopeActionContext
        {
            ThrowIfDisposed();
            if (extendState == null) throw new ArgumentNullException(nameof(extendState));
            _executionRuntime.Register(rule, executionMode, (eventState, groupState) =>
                extendState(new EcaExecutionRuleState<E>(eventState, groupState), State));
        }

        public void Register<E, C, A>(IEcaRule<E, EcaScopeRuleState<E>, C, A> rule, EcaExecutionMode executionMode)
            where C : IEcaScopeConditionContext
            where A : IEcaScopeActionContext
        {
            Register(rule, executionMode, (executionState, scopeState) => new EcaScopeRuleState<E>(
                executionState.EventState, executionState.ExecutionGroupState, scopeState));
        }

        public void Fire<E, R, C, A>(IEcaEvent<E> ecaEvent, E eventState, C conditionContext, A actionContext)
            where R : IEcaScopeRuleState<E>
            where C : IEcaScopeConditionContext
            where A : IEcaScopeActionContext
        {
            ThrowIfDisposed();
            _executionRuntime.Fire<E, R, C, A>(ecaEvent, eventState, conditionContext, actionContext);
        }

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

        public void ForceFire<E, R, C, A>(
            IEcaEvent<E> ecaEvent, E eventState, Func<IEcaRule<E, R, C, A>, E, R> createState,
            C conditionContext, A actionContext)
            where R : IEcaRuleState<E>
            where C : IEcaConditionContext
            where A : IEcaActionContext
        {
            ThrowIfDisposed();
            _executionRuntime.ForceFire(ecaEvent, eventState, createState, conditionContext, actionContext);
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
