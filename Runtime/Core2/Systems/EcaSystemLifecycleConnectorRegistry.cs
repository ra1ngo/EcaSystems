using System;
using System.Collections.Generic;

namespace EcaSystems.Core2
{
    public sealed class EcaSystemLifecycleConnectorRegistry
    {
        private readonly SortedDictionary<string, IEcaSystemLifecycleConnector> _connectors = new(StringComparer.Ordinal);
        public IReadOnlyList<IEcaSystemLifecycleConnector> GetSnapshot() =>
            new List<IEcaSystemLifecycleConnector>(_connectors.Values).AsReadOnly();

        public void Register(string systemId, IEcaSystemLifecycleConnector connector)
        {
            ValidateId(systemId);
            if (connector == null) throw new ArgumentNullException(nameof(connector));
            if (_connectors.ContainsKey(systemId)) throw new InvalidOperationException("System lifecycle connector is already registered.");
            foreach (var existing in _connectors.Values)
                if (ReferenceEquals(existing, connector)) throw new InvalidOperationException("Connector is already owned by a registered System.");
            _connectors.Add(systemId, connector);
        }

        public bool Unregister(string systemId) { ValidateId(systemId); return _connectors.Remove(systemId); }
        public bool Contains(string systemId) { ValidateId(systemId); return _connectors.ContainsKey(systemId); }
        public IEcaSystemLifecycleConnector Resolve(string systemId)
        {
            ValidateId(systemId);
            if (_connectors.TryGetValue(systemId, out var connector)) return connector;
            throw new InvalidOperationException("System lifecycle connector is not registered.");
        }
        public bool CheckRegistered(string systemId, IEcaSystemLifecycleConnector connector) =>
            systemId != null && connector != null && _connectors.TryGetValue(systemId, out var current) && ReferenceEquals(current, connector);
        private static void ValidateId(string systemId)
        {
            if (string.IsNullOrWhiteSpace(systemId)) throw new ArgumentException("System id cannot be empty.", nameof(systemId));
        }
    }
}
