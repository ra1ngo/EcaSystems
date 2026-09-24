using System;
using System.Collections.Generic;

namespace EcaSystems.Variables
{
    public sealed class EcaVariablesSystem
    {
        private readonly Dictionary<string, EcaVariableStore> _stores = new(StringComparer.Ordinal);

        public event Action<EcaVariableChanged> VariableChanged;

        public EcaVariablesSystemState GetState()
        {
            var stores = new List<EcaVariableStoreState>(_stores.Count);
            foreach (var store in _stores.Values) stores.Add(store.GetStoreState());
            return new EcaVariablesSystemState(stores);
        }

        public EcaVariableStore CreateStore(string storeId, string parentId = null)
        {
            ValidateId(storeId, nameof(storeId));
            if (parentId != null) ValidateId(parentId, nameof(parentId));
            if (_stores.ContainsKey(storeId))
                throw new InvalidOperationException($"Store '{storeId}' already exists.");
            if (parentId != null && !_stores.ContainsKey(parentId))
                throw new InvalidOperationException($"Parent store '{parentId}' does not exist.");
            var store = new EcaVariableStore(this, storeId, parentId);
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

        internal void PublishVariableChanged(EcaVariableChanged change) => VariableChanged?.Invoke(change);

        internal IReadOnlyList<EcaVariableStore> GetChildren(string parentId)
        {
            var children = new List<EcaVariableStore>();
            foreach (var store in _stores.Values)
                if (store.ParentId == parentId) children.Add(store);
            return children.AsReadOnly();
        }

        internal IReadOnlyList<EcaVariableStore> GetSiblings(EcaVariableStore source)
        {
            var siblings = new List<EcaVariableStore>();
            foreach (var store in _stores.Values)
                if (store != source && store.ParentId == source.ParentId) siblings.Add(store);
            return siblings.AsReadOnly();
        }

        internal IReadOnlyList<EcaVariableStore> GetSubtree(EcaVariableStore root)
        {
            var subtree = new List<EcaVariableStore> { root };
            for (var index = 0; index < subtree.Count; index++)
            {
                var parentId = subtree[index].Id;
                foreach (var store in _stores.Values)
                    if (store.ParentId == parentId) subtree.Add(store);
            }
            return subtree.AsReadOnly();
        }

        private static void ValidateId(string id, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Store id cannot be empty.", parameterName);
        }
    }
}
