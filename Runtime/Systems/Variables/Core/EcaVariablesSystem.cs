using System;
using System.Collections.Generic;

namespace EcaSystems.Variables
{
    public sealed class EcaVariablesSystem
    {
        private readonly Dictionary<string, EcaVariableStore> _stores = new(StringComparer.Ordinal);

        public EcaVariableStore CreateStore(string storeId, string parentId = null)
        {
            ValidateId(storeId, nameof(storeId));
            if (parentId != null) ValidateId(parentId, nameof(parentId));
            if (_stores.ContainsKey(storeId))
                throw new InvalidOperationException($"Store '{storeId}' already exists.");
            if (parentId != null && !_stores.ContainsKey(parentId))
                throw new InvalidOperationException($"Parent store '{parentId}' does not exist.");
            var store = new EcaVariableStore(storeId, parentId);
            _stores.Add(storeId, store);
            return store;
        }

        public bool ContainsStore(string storeId)
        {
            ValidateId(storeId, nameof(storeId));
            return _stores.ContainsKey(storeId);
        }

        public EcaVariableStore GetStore(string storeId)
        {
            ValidateId(storeId, nameof(storeId));
            if (!_stores.TryGetValue(storeId, out var store))
                throw new InvalidOperationException($"Store '{storeId}' does not exist.");
            return store;
        }

        public bool TryGetStore(string storeId, out EcaVariableStore store)
        {
            ValidateId(storeId, nameof(storeId));
            return _stores.TryGetValue(storeId, out store);
        }

        private static void ValidateId(string id, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Store id cannot be empty.", parameterName);
        }
    }
}
