using System;
using System.Collections.Generic;

namespace EcaSystems.Core2
{
    public sealed class EcaSystemNamespaceRegistry
    {
        public IReadOnlyList<EcaSystemNamespace> GetSnapshot() => new List<EcaSystemNamespace>(_namespaces.Values).AsReadOnly();

        private readonly Dictionary<string, EcaSystemNamespace> _namespaces = new(StringComparer.Ordinal);
        public IReadOnlyCollection<EcaSystemNamespace> Namespaces => _namespaces.Values;

        public void Register(EcaSystemNamespace systemNamespace)
        {
            if (systemNamespace == null) throw new ArgumentNullException(nameof(systemNamespace));
            ValidateId(systemNamespace.Id);
            if (_namespaces.ContainsKey(systemNamespace.Id)) throw new InvalidOperationException($"namespace '{systemNamespace.Id}' is already registered.");
            _namespaces.Add(systemNamespace.Id, systemNamespace);
        }

        public bool Unregister(string namespaceId)
        {
            ValidateId(namespaceId);
            return _namespaces.Remove(namespaceId);
        }

        public bool Contains(string namespaceId)
        {
            ValidateId(namespaceId);
            return _namespaces.ContainsKey(namespaceId);
        }

        public EcaSystemNamespace Resolve(string namespaceId)
        {
            ValidateId(namespaceId);
            if (_namespaces.TryGetValue(namespaceId, out var item)) return item;
            throw new InvalidOperationException($"namespace '{namespaceId}' is not registered.");
        }

        public bool CheckRegistered(EcaSystemNamespace systemNamespace) =>
            systemNamespace != null && systemNamespace.Id != null &&
            _namespaces.TryGetValue(systemNamespace.Id, out var registered) && ReferenceEquals(registered, systemNamespace);

        private static void ValidateId(string namespaceId)
        {
            if (string.IsNullOrWhiteSpace(namespaceId)) throw new ArgumentException("namespace id cannot be empty or whitespace.", nameof(namespaceId));
        }
    }
}
