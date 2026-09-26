using System;
using System.Collections.Generic;

namespace EcaSystems.Core2
{
    public sealed class EcaSystemRegistry
    {
        // Frozen membership, retaining the original entry references.
        public IReadOnlyList<EcaSystem> GetSnapshot() => new List<EcaSystem>(_systems.Values).AsReadOnly();

        private readonly Dictionary<string, EcaSystem> _systems = new(StringComparer.Ordinal);
        public IReadOnlyCollection<EcaSystem> Systems => _systems.Values;

        public void Register(EcaSystem system)
        {
            if (system == null) throw new ArgumentNullException(nameof(system));
            ValidateId(system.Id);
            if (_systems.ContainsKey(system.Id)) throw new InvalidOperationException($"system '{system.Id}' is already registered.");
            _systems.Add(system.Id, system);
        }

        public bool Unregister(string systemId)
        {
            ValidateId(systemId);
            return _systems.Remove(systemId);
        }

        public bool Contains(string systemId)
        {
            ValidateId(systemId);
            return _systems.ContainsKey(systemId);
        }

        public EcaSystem Resolve(string systemId)
        {
            ValidateId(systemId);
            if (_systems.TryGetValue(systemId, out var item)) return item;
            throw new InvalidOperationException($"system '{systemId}' is not registered.");
        }

        public bool CheckRegistered(EcaSystem system) =>
            system != null && system.Id != null &&
            _systems.TryGetValue(system.Id, out var registered) && ReferenceEquals(registered, system);

        private static void ValidateId(string systemId)
        {
            if (string.IsNullOrWhiteSpace(systemId)) throw new ArgumentException("system id cannot be empty or whitespace.", nameof(systemId));
        }
    }
}
