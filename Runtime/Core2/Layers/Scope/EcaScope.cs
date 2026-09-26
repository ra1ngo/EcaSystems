using System;
using System.Collections.Generic;

namespace EcaSystems.Core2
{
    public sealed class EcaScope : IDisposable, IEcaEventHandler
    {
        private readonly EcaScopeRuntime _owner;
        private EcaExecutionRuntime _executionRuntime;
        private IEcaRuleRegistry _rules;
        internal IReadOnlyList<IEcaRule> GetRulesSnapshot() => _rules.GetSnapshot();

        public IEcaEventEmitter EventEmitter { get; }
        public EcaScopeState State { get; }
        public string ScopeId => State.ScopeId;
        public string ParentScopeId { get; }
        public bool IsDisposed { get; private set; }

        internal EcaScope(EcaScopeRuntime owner, EcaScopeState state, IEcaRuleRegistry rules, IEcaExecutionGroupRegistry groups, IEcaConditionChecker conditionChecker, IEcaActionRunner actionRunner, string parentScopeId)
        {
            _owner = owner;
            _rules = rules;
            ParentScopeId = parentScopeId;
            State = state ?? throw new ArgumentNullException(nameof(state));
            _executionRuntime = new EcaExecutionRuntime(rules, groups,
                conditionChecker, actionRunner);
            var emitter = new EcaEventEmitter();
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
            _owner.Mutate(() =>
            {
                _executionRuntime.Register(rule, executionMode, (eventState, groupState) =>
                    extendState(new EcaExecutionRuleState<E>(rule.Id, eventState, groupState), State));
                try { _owner.Lifecycle.ConnectRule(this, rule); }
                catch { _executionRuntime.Unregister(rule); throw; }
            });
        }

        public void Register<E>(IEcaRule<E, EcaScopeRuleState<E>> rule, EcaExecutionMode executionMode)
        {
            Register(rule, executionMode, (executionState, scopeState) => new EcaScopeRuleState<E>(
                executionState.RuleId, executionState.EventState, executionState.ExecutionGroupState, scopeState));
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
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            var removed = false;
            _owner.Mutate(() =>
            {
                foreach (var registered in _rules.GetSnapshot())
                {
                    if (!ReferenceEquals(registered, rule)) continue;
                    _owner.Lifecycle.DisconnectRule(this, rule);
                    removed = _executionRuntime.Unregister(rule);
                    break;
                }
            });
            return removed;
        }

        public void ForceFire<E, R>(
            IEcaEvent<E> ecaEvent, E eventState, Func<IEcaRule<E, R>, E, R> createState,
            IEcaConditionContext conditionContext = null, IEcaActionContext actionContext = null)
            where R : IEcaRuleState<E>
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
            _owner.DisposeScope(this);
        }

        internal void CommitDispose()
        {
            IsDisposed = true;
            // Новые операции закрыты; текущие Fire и Action Tasks удерживают свои Groups до завершения.
            _executionRuntime = null;
            _rules = null;
        }

        private void ThrowIfDisposed()
        {
            if (IsDisposed) throw new ObjectDisposedException(nameof(EcaScope));
        }
    }
}
