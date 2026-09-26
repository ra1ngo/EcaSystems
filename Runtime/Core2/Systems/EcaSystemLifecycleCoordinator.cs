using System;
using System.Collections.Generic;

namespace EcaSystems.Core2
{
    public sealed class EcaSystemLifecycleCoordinator : IEcaScopeLifecycleListener
    {
        private readonly EcaSystemLifecycleConnectorRegistry _connectors;
        private readonly EcaScopeRuntime _scopes;

        public EcaSystemLifecycleCoordinator(EcaSystemLifecycleConnectorRegistry connectors, EcaScopeRuntime scopes)
        {
            _connectors = connectors ?? throw new ArgumentNullException(nameof(connectors));
            _scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
        }

        public void ConnectScope(EcaScope scope) => EcaScopeLifecycle.Apply(_connectors.GetSnapshot(),
            c => c.ConnectScope(scope), c => c.DisconnectScope(scope));
        public void DisconnectScope(EcaScope scope) => EcaScopeLifecycle.Apply(_connectors.GetSnapshot(),
            c => c.DisconnectScope(scope), c => RestoreScope(c, scope));
        public void ConnectRule(EcaScope scope, IEcaRule rule) => EcaScopeLifecycle.Apply(_connectors.GetSnapshot(),
            c => c.ConnectRule(scope, rule), c => c.DisconnectRule(scope, rule));
        public void DisconnectRule(EcaScope scope, IEcaRule rule) => EcaScopeLifecycle.Apply(_connectors.GetSnapshot(),
            c => c.DisconnectRule(scope, rule), c => c.ConnectRule(scope, rule));

        // Caller holds the runtime topology mutation guard. No second topology/Rule list is retained.
        internal void Connect(IEcaSystemLifecycleConnector connector)
        {
            if (connector == null) return;
            var undo = new Stack<Action>();
            try
            {
                foreach (var scope in _scopes.GetSnapshot())
                {
                    connector.ConnectScope(scope);
                    var connectedScope = scope;
                    undo.Push(() => connector.DisconnectScope(connectedScope));
                    foreach (var rule in scope.GetRulesSnapshot())
                    {
                        connector.ConnectRule(scope, rule);
                        var connectedRule = rule;
                        undo.Push(() => connector.DisconnectRule(connectedScope, connectedRule));
                    }
                }
            }
            catch (Exception failure) { EcaScopeLifecycle.Rollback(undo, failure); throw; }
        }

        internal void Disconnect(IEcaSystemLifecycleConnector connector)
        {
            if (connector == null) return;
            var scopes = new List<EcaScope>(_scopes.GetSnapshot());
            scopes.Reverse();
            EcaScopeLifecycle.Apply(scopes, connector.DisconnectScope, scope => RestoreScope(connector, scope));
        }

        private static void RestoreScope(IEcaSystemLifecycleConnector connector, EcaScope scope)
        {
            connector.ConnectScope(scope);
            try { foreach (var rule in scope.GetRulesSnapshot()) connector.ConnectRule(scope, rule); }
            catch (Exception failure)
            {
                var undo = new Stack<Action>();
                undo.Push(() => connector.DisconnectScope(scope));
                EcaScopeLifecycle.Rollback(undo, failure);
                throw;
            }
        }
    }
}
