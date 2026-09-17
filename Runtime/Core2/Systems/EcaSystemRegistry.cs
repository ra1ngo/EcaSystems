using System;
using System.Collections.Generic;

namespace EcaSystems.Core2
{
    public sealed class EcaSystemRegistry
    {
        private readonly Dictionary<string, EcaSystem> _systems = new(StringComparer.Ordinal);

        public void Register(EcaSystem system)
        {
            if (system == null) throw new ArgumentNullException(nameof(system));
            ValidateId(system.Id);
            if (_systems.ContainsKey(system.Id)) throw new InvalidOperationException($"System '{system.Id}' is already registered.");
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

        internal EcaSystem Resolve(string systemId)
        {
            ValidateId(systemId);
            if (_systems.TryGetValue(systemId, out var system)) return system;
            throw new InvalidOperationException($"System '{systemId}' is not registered.");
        }

        private static void ValidateId(string systemId)
        {
            if (string.IsNullOrWhiteSpace(systemId)) throw new ArgumentException("System id cannot be empty.", nameof(systemId));
        }
    }
}
