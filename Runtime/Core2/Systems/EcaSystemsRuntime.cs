using System;
using System.Collections.Generic;

namespace EcaSystems.Core2
{
    /// <summary>Owns global exports and scopes; does not process Fire or own external adapters.</summary>
    public sealed class EcaSystemsRuntime : IDisposable
    {
        private readonly EcaSystemRegistry _systems;
        private readonly EcaSystemConnector _connector;
        private readonly EcaCommandRunner _commandRunner;
        private readonly EcaScopeRuntime _scopes;
        private bool _isDisposed;

        public EcaSystemsRuntime()
        {
            var events = new EcaBaseEventRegistry();
            var commands = new EcaCommandRegistry();
            _systems = new EcaSystemRegistry();
            var namespaces = new EcaSystemNamespaceRegistry();
            _connector = new EcaSystemConnector(_systems, namespaces, events, commands);
            _commandRunner = new EcaCommandRunner(commands);
            _scopes = new EcaScopeRuntime(events);
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
