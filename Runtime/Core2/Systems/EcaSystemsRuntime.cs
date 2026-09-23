using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EcaSystems.Core2
{
    /// <summary>Owns global exports and scopes; does not process Fire or own external adapters.</summary>
    public sealed class EcaSystemsRuntime : IDisposable
    {
        private readonly EcaBaseEventRegistry _events;
        private readonly EcaStateRegistry _states;
        private readonly EcaStateResolver _stateResolver;
        private readonly EcaBaseConditionChecker _conditionChecker;
        private readonly EcaBaseActionRunner _actionRunner;
        private readonly EcaRuleCreator _ruleCreator;
        private readonly EcaSystemRegistry _systems;
        private readonly EcaSystemConnector _connector;
        private readonly EcaCommandRunner _commandRunner;
        private readonly EcaScopeRuntime _scopes;
        private bool _isDisposed;

        public EcaSystemsRuntime()
        {
            _events = new EcaBaseEventRegistry();
            var commands = new EcaCommandRegistry();
            _states = new EcaStateRegistry();
            _stateResolver = new EcaStateResolver(_states);
            _systems = new EcaSystemRegistry();
            var namespaces = new EcaSystemNamespaceRegistry();
            _connector = new EcaSystemConnector(_systems, namespaces, _events, commands, _states);
            _commandRunner = new EcaCommandRunner(commands);
            _conditionChecker = new EcaBaseConditionChecker();
            _actionRunner = new EcaBaseActionRunner();
            _ruleCreator = new EcaRuleCreator(_events, _commandRunner, _stateResolver);
            _scopes = new EcaScopeRuntime(_events, _conditionChecker, _actionRunner);
        }

        public void ConnectSystem(EcaSystem system)
        {
            ThrowIfDisposed();
            _connector.Connect(system);
        }

        public void DisconnectSystem(EcaSystem system)
        {
            ThrowIfDisposed();
            _connector.Disconnect(system);
        }

        public EcaScope CreateScope(string scopeId = null)
        {
            ThrowIfDisposed();
            return _scopes.CreateScope(scopeId);
        }

        public EcaRule<E, EcaScopeRuleState<E>> CreateRule<E, C, A>(string id, string eventId)
            where C : AEcaCondition<EcaScopeRuleState<E>>, new()
            where A : AEcaAction<EcaScopeRuleState<E>>, new()
        {
            ThrowIfDisposed();
            return _ruleCreator.Create<E, EcaScopeRuleState<E>, C, A>(id, eventId);
        }

        public EcaRule<E, R> CreateRule<E, R, C, A>(string id, string eventId)
            where R : IEcaRuleState<E>
            where C : AEcaCondition<R>, new()
            where A : AEcaAction<R>, new()
        {
            ThrowIfDisposed();
            return _ruleCreator.Create<E, R, C, A>(id, eventId);
        }

        public EcaRule<E, EcaScopeRuleState<E>> CreateRule<E, A>(string id, string eventId)
            where A : AEcaAction<EcaScopeRuleState<E>>, new()
        {
            ThrowIfDisposed();
            return _ruleCreator.Create<E, EcaScopeRuleState<E>, A>(id, eventId);
        }

        public EcaRule<E, EcaScopeRuleState<E>> CreateRule<E>(string id, string eventId,
            Func<EcaScopeRuleState<E>, IEcaActionContext, IEcaCommands, Task> action,
            Func<EcaScopeRuleState<E>, IEcaConditionContext, bool> condition = null)
        {
            ThrowIfDisposed();
            return _ruleCreator.Create<E, EcaScopeRuleState<E>>(id, eventId, action, condition);
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            List<Exception> failures = null;
            try { _scopes.Dispose(); }
            catch (Exception error) { (failures ??= new()).Add(error); }

            // Systems is a live view: disconnect against a snapshot, continuing after failures.
            foreach (var system in new List<EcaSystem>(_systems.Systems))
            {
                try { _connector.Disconnect(system); }
                catch (Exception error) { (failures ??= new()).Add(error); }
            }
            if (failures != null)
                throw new AggregateException("Runtime cleanup could not disconnect all owned resources.", failures);
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(EcaSystemsRuntime));
        }
    }
}
