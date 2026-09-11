using System;

namespace EcaSystems.Core
{
    public sealed class EcaScope : IDisposable
    {
        private readonly EcaScopeEngine _owner;
        private EcaExecutionEngine _executionEngine;

        public EcaScopeState State { get; }
        public string ScopeId => State.ScopeId;
        public string ParentScopeId { get; }
        public bool IsDisposed { get; private set; }

        internal EcaScope(EcaScopeEngine owner, EcaExecutionEngine executionEngine, string scopeId, string parentScopeId)
        {
            _owner = owner;
            _executionEngine = executionEngine ?? throw new ArgumentNullException(nameof(executionEngine));
            State = new EcaScopeState(scopeId);
            ParentScopeId = parentScopeId;
        }

        public EcaScope CreateScope(string scopeId = null)
        {
            ThrowIfDisposed();
            return _owner.CreateScope(this, scopeId);
        }

        public void Register<TEventContext>(IEcaRule<TEventContext, IEcaExecutionConditionContext<TEventContext>, IEcaExecutionActionContext<TEventContext>> rule,
            EcaRunMode runMode)
        {
            ThrowIfDisposed();
            _executionEngine.Register(rule, runMode);
        }

        public bool Unregister<TEventContext>(IEcaRule<TEventContext, IEcaExecutionConditionContext<TEventContext>, IEcaExecutionActionContext<TEventContext>> rule)
        {
            ThrowIfDisposed();
            return _executionEngine.Unregister(rule);
        }

        /* Иерархия определяет владение и lifetime. Fire локален:
           ни родитель, ни потомки автоматически не получают событие. */
        public void Fire(EcaEvent<EcaEventContextEmpty> ecaEvent)
        {
            ThrowIfDisposed();
            _executionEngine.Fire(ecaEvent);
        }

        public void Fire<TEventContext>(EcaEvent<TEventContext> ecaEvent, TEventContext eventContext)
        {
            ThrowIfDisposed();
            _executionEngine.Fire(ecaEvent, eventContext);
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            _owner.DisposeScope(this);
            /* Dispose закрывает регистрации, но не может принудительно остановить Task.
               Запущенные Actions удерживают свои группы и завершаются самостоятельно. */
            _executionEngine = null;
        }

        private void ThrowIfDisposed()
        {
            if (IsDisposed) throw new ObjectDisposedException(nameof(EcaScope));
        }
    }
}
