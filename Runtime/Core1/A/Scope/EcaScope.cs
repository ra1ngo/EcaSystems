using System;

namespace EcaSystems.Core1
{
    public sealed class EcaScope : IDisposable
    {
        private readonly EcaScopeEngine _owner;
        private readonly EcaExecutionEngine _executionEngine;

        public EcaScopeState State { get; }
        public string ScopeId => State.ScopeId;
        public string ParentScopeId { get; }
        public bool IsDisposed { get; private set; }

        internal EcaScope(EcaScopeEngine owner, EcaExecutionEngine executionEngine, EcaScopeState state, string parentScopeId)
        {
            _owner = owner;
            _executionEngine = executionEngine;
            State = state;
            ParentScopeId = parentScopeId;
        }

        public EcaScope CreateScope(string scopeId = null)
        {
            ThrowIfDisposed();
            return _owner.CreateScope(this, scopeId);
        }

        public void Register<T>(IEcaRule<T, EcaScopeRuleState<T>, IEcaScopeConditionRunnerContext,
            IEcaScopeActionRunnerContext> rule, EcaExecutionMode mode)
        {
            ThrowIfDisposed();
            _executionEngine.RegisterCore(rule, mode);
        }

        public bool Unregister(IEcaRule rule)
        {
            ThrowIfDisposed();
            return _executionEngine.Unregister(rule);
        }

        public void Fire(IEcaEvent<EcaEventStateEmpty> ecaEvent) => Fire(ecaEvent, default);

        public void Fire<T>(IEcaEvent<T> ecaEvent, T eventState)
        {
            ThrowIfDisposed();
            /* Иерархия задаёт ownership/lifetime. Fire обрабатывает только этот Scope. */
            _executionEngine.Fire(ecaEvent, eventState);
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            _owner.DisposeScope(this);
            /* Отписка закрывает новые Fire; активные Action Tasks не отменяются. */
            _executionEngine.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (IsDisposed) throw new ObjectDisposedException(nameof(EcaScope));
        }
    }
}
