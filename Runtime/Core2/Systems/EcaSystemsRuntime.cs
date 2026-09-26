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
        private bool _disposing;
        private readonly EcaSystemLifecycleCoordinator _lifecycle;

        public EcaSystemsRuntime()
        {
            _events = new EcaBaseEventRegistry();
            var commands = new EcaCommandRegistry();
            _states = new EcaStateRegistry();
            _stateResolver = new EcaStateResolver(_states);
            _systems = new EcaSystemRegistry();
            var namespaces = new EcaSystemNamespaceRegistry();
            var connectors = new EcaSystemLifecycleConnectorRegistry();
            _connector = new EcaSystemConnector(_systems, namespaces, _events, commands, _states, connectors);
            _commandRunner = new EcaCommandRunner(commands);
            _conditionChecker = new EcaBaseConditionChecker();
            _actionRunner = new EcaBaseActionRunner();
            _ruleCreator = new EcaRuleCreator(_events, _commandRunner, _stateResolver);
            var lifecycle = new EcaScopeLifecycle();
            _scopes = new EcaScopeRuntime(_events, _conditionChecker, _actionRunner, lifecycle);
            _lifecycle = new EcaSystemLifecycleCoordinator(connectors, _scopes);
            lifecycle.Register(_lifecycle);
        }

        public void ConnectSystem(EcaSystem system)
        {
            ThrowIfDisposed();
            _scopes.Mutate(() =>
            {
                _connector.Connect(system);
                try { _lifecycle.Connect(system.LifecycleConnector); }
                catch (Exception failure)
                {
                    var undo = new Stack<Action>();
                    undo.Push(() => _connector.Disconnect(system));
                    EcaScopeLifecycle.Rollback(undo, failure);
                    throw;
                }
            });
        }

        public void DisconnectSystem(EcaSystem system)
        {
            ThrowIfDisposed();
            _scopes.Mutate(() =>
            {
                if (system == null) throw new ArgumentNullException(nameof(system));
                if (!_systems.CheckRegistered(system)) throw new InvalidOperationException("System is not the connected instance.");
                _lifecycle.Disconnect(system.LifecycleConnector);
                try { _connector.Disconnect(system); }
                catch (Exception failure)
                {
                    var undo = new Stack<Action>();
                    undo.Push(() => _lifecycle.Connect(system.LifecycleConnector));
                    EcaScopeLifecycle.Rollback(undo, failure);
                    throw;
                }
            });
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
            if (_disposing) throw new InvalidOperationException("Runtime is already disposing.");
            _disposing = true;
            try { _scopes.Dispose(); }
            catch { _disposing = false; throw; }
            _isDisposed = true;
            _disposing = false;
            List<Exception> failures = null;

            // Systems is a live view: disconnect against a snapshot, continuing after failures.
            foreach (var system in _systems.GetSnapshot())
            {
                try { _connector.Disconnect(system); }
                catch (Exception error) { (failures ??= new()).Add(error); }
            }
            if (failures != null)
                throw new AggregateException("Runtime cleanup could not disconnect all owned resources.", failures);
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed || _disposing) throw new ObjectDisposedException(nameof(EcaSystemsRuntime));
        }
    }
}
