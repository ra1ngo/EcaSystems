using System;
using System.Collections.Generic;

namespace EcaSystems.Variables
{
    public sealed class EcaVariableStore
    {
        private readonly EcaVariablesSystem _system;
        private readonly Dictionary<string, EcaVariable> _variables = new(StringComparer.Ordinal);
        private readonly EcaVariableSnapshotMapper _snapshotMapper = new();
        public string Id { get; }
        public string ParentId { get; }
        public bool IsRoot => ParentId == null;
        public event Action<EcaVariableChanged> VariableChanged;

        internal EcaVariableStore(EcaVariablesSystem system, string id, string parentId)
        {
            _system = system;
            Id = id;
            ParentId = parentId;
        }

        public EcaVariable Declare<T>(string variableId, T defaultValue)
        {
            ValidateInput<T>(variableId);
            if (_variables.ContainsKey(variableId))
                throw new InvalidOperationException($"Variable '{variableId}' is already declared.");
            var definition = new EcaVariableDefinition(variableId, typeof(T), defaultValue);
            var variable = new EcaVariable(new EcaVariableData(Id, definition), this);
            _variables.Add(variableId, variable);
            return variable;
        }

        public bool Contains(string variableId)
        {
            ValidateId(variableId);
            return _variables.ContainsKey(variableId);
        }

        public EcaVariable GetVariable(string variableId)
        {
            ValidateId(variableId);
            if (!_variables.TryGetValue(variableId, out var variable))
                throw new InvalidOperationException($"Variable '{variableId}' is not declared.");
            return variable;
        }

        public bool TryGetVariable(string variableId, out EcaVariable variable)
        {
            ValidateId(variableId);
            return _variables.TryGetValue(variableId, out variable);
        }

        public T GetValue<T>(string variableId) => Resolve<T>(variableId).GetValue<T>();

        public bool TryGetValue<T>(string variableId, out T value)
        {
            ValidateInput<T>(variableId);
            value = default;
            if (!TryGetVariable(variableId, out var variable)) return false;
            value = variable.GetValue<T>();
            return true;
        }

        public void SetValue<T>(string variableId, T value) => Resolve<T>(variableId).SetValue(value);
        public void ForceSetValue<T>(string variableId, T value) => Resolve<T>(variableId).ForceSetValue(value);
        public void SetCurrentValue<T>(string variableId, T value) => Resolve<T>(variableId).SetCurrentValue(value);

        public EcaVariableStore GetParent() => IsRoot ? null : _system.GetStore(ParentId);

        public bool TryGetParent(out EcaVariableStore parent)
        {
            parent = GetParent();
            return parent != null;
        }

        public IReadOnlyList<EcaVariableStore> GetChildren() => _system.GetChildren(Id);
        public IReadOnlyList<EcaVariableStore> GetSiblings() => _system.GetSiblings(this);
        public IReadOnlyList<EcaVariableStore> GetSubtree() => _system.GetSubtree(this);

        public EcaVariableStoreState GetStoreState()
        {
            var variables = new List<EcaVariableSnapshot>(_variables.Count);
            foreach (var variable in _variables.Values) variables.Add(_snapshotMapper.Map(variable));
            return new EcaVariableStoreState(Id, ParentId, variables);
        }

        public EcaVariableSubtreeState GetSubtreeState()
        {
            var stores = new List<EcaVariableStoreState>();
            foreach (var store in GetSubtree()) stores.Add(store.GetStoreState());
            return new EcaVariableSubtreeState(Id, stores);
        }

        internal void BubbleVariableChanged(EcaVariableChanged change)
        {
            // Authoritative parent lookup, never public-event subscription wiring.
            for (var store = this; store != null; store = store.GetParent())
                store.VariableChanged?.Invoke(change);
            _system.PublishVariableChanged(change);
        }

        private EcaVariable Resolve<T>(string variableId)
        {
            ValidateInput<T>(variableId);
            return GetVariable(variableId);
        }

        private static void ValidateInput<T>(string variableId)
        {
            ValidateId(variableId);
            EcaVariableController.ValidateSupportedType<T>();
        }

        private static void ValidateId(string variableId)
        {
            if (string.IsNullOrWhiteSpace(variableId))
                throw new ArgumentException("Variable id cannot be empty.", nameof(variableId));
        }
    }
}
