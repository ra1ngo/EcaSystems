using System;
using System.Collections.Generic;

namespace EcaSystems.Core2
{
    public sealed class EcaSystemNamespaceRegistry
    {
        private readonly Dictionary<string, EcaSystemNamespace> _namespaces = new(StringComparer.Ordinal);

        public void Register(EcaSystemNamespace systemNamespace)
        {
            if (systemNamespace == null) throw new ArgumentNullException(nameof(systemNamespace));
            ValidateId(systemNamespace.Id);
            if (_namespaces.ContainsKey(systemNamespace.Id))
                throw new InvalidOperationException($"Namespace '{systemNamespace.Id}' is already registered.");
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

        private static void ValidateId(string namespaceId)
        {
            if (string.IsNullOrWhiteSpace(namespaceId)) throw new ArgumentException("Namespace id cannot be empty.", nameof(namespaceId));
        }
    }
}
