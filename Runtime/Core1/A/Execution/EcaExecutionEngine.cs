using System;

namespace EcaSystems.Core1
{
    /* Композиция одного Base pipeline. Execution не выбирает Rules и не проверяет
       Conditions самостоятельно; он добавляет Group через текущий RuleRunner. */
    public sealed class EcaExecutionEngine : IDisposable
    {
        private readonly EcaEventDispatcher _dispatcher;
        private readonly EcaBaseEngine _baseEngine;
        private readonly EcaRuleExecutionRegistry _groups;
        private bool _isDisposed;

        public EcaExecutionEngine(EcaEventRegistry events) : this(events, new EcaRuleExecutionRegistry()) { }

        private EcaExecutionEngine(EcaEventRegistry events, EcaRuleExecutionRegistry groups)
            : this(events, groups, new EcaExecutionRuleRunner(groups)) { }

        internal EcaExecutionEngine(EcaEventRegistry events, EcaRuleExecutionRegistry groups, EcaExecutionRuleRunner runner)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            _groups = groups ?? throw new ArgumentNullException(nameof(groups));
            _dispatcher = new EcaEventDispatcher(events);
            _baseEngine = new EcaBaseEngine(_dispatcher, new EcaRuleRegistry(events), runner);
        }

        public void Register<T>(IEcaRule<T, EcaExecutionRuleState<T>, IEcaExecutionConditionRunnerContext,
            IEcaExecutionActionRunnerContext> rule, EcaExecutionMode mode) => RegisterCore(rule, mode);

        internal void RegisterCore<T, TS, TC, TA>(IEcaRule<T, TS, TC, TA> rule, EcaExecutionMode mode)
            where TS : IEcaExecutionRuleState<T>
            where TC : IEcaExecutionConditionRunnerContext
            where TA : IEcaExecutionActionRunnerContext
        {
            ThrowIfDisposed();
            _baseEngine.Register(rule);
            try { _groups.Register(rule, mode); }
            catch
            {
                _baseEngine.Unregister(rule);
                throw;
            }
        }

        public bool Unregister(IEcaRule rule)
        {
            ThrowIfDisposed();
            if (!_baseEngine.Unregister(rule)) return false;
            _groups.Remove(rule.Id);
            return true;
        }

        public bool TryGetGroup(string ruleId, out EcaRuleExecutionGroup group) => _groups.TryGet(ruleId, out group);

        public void Fire(IEcaEvent<EcaEventStateEmpty> ecaEvent) => Fire(ecaEvent, default);

        public void Fire<T>(IEcaEvent<T> ecaEvent, T eventState)
        {
            ThrowIfDisposed();
            _dispatcher.Fire(ecaEvent, eventState);
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _baseEngine.Dispose();
            /* Активные Tasks удерживают свои Group и завершаются естественно. */
            _groups.Clear();
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(EcaExecutionEngine));
        }
    }
}
