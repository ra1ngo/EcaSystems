using System;
using System.Collections.Generic;

namespace EcaSystems.Variables
{
    public sealed class EcaVariableStore
    {
        private readonly EcaVariablesSystem _system;
        private readonly EcaVariableSnapshotMapper _snapshotMapper = new();
        public EcaVariableRegistry Variables { get; } = new();
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
            EcaVariableRegistry.ValidateId(variableId);
            EcaVariableController.ValidateSupportedType<T>();
            var definition = new EcaVariableDefinition(variableId, typeof(T), defaultValue);
            var variable = new EcaVariable(new EcaVariableData(Id, definition), this);
            Variables.Register(variable);
            return variable;
        }

        public bool Contains(string variableId) => Variables.Contains(variableId);

        public EcaVariable GetVariable(string variableId) => Variables.Resolve(variableId);

        public bool TryGetVariable(string variableId, out EcaVariable variable)
            => Variables.TryResolve(variableId, out variable);

        public T GetValue<T>(string variableId) => Variables.Resolve(variableId).GetValue<T>();

        public bool TryGetValue<T>(string variableId, out T value)
        {
            value = default;
            if (!Variables.TryResolve(variableId, out var variable)) return false;
            value = variable.GetValue<T>();
            return true;
        }

        public void SetValue<T>(string variableId, T value) => Variables.Resolve(variableId).SetValue(value);
        public void ForceSetValue<T>(string variableId, T value) => Variables.Resolve(variableId).ForceSetValue(value);
        public void SetCurrentValue<T>(string variableId, T value) => Variables.Resolve(variableId).SetCurrentValue(value);

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
            var variables = new List<EcaVariableSnapshot>(Variables.Variables.Count);
            foreach (var variable in Variables.Variables) variables.Add(_snapshotMapper.Map(variable));
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
    }
}
