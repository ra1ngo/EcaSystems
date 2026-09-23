using System;
using System.Collections.Generic;

namespace EcaSystems.Variables
{
    public sealed class EcaVariablesSystem
    {
        private readonly Dictionary<string, EcaVariable> _variables = new(StringComparer.Ordinal);
        public event Action<EcaVariableChanged> VariableChanged;

        public EcaVariable Declare<T>(string variableId, T defaultValue)
        {
            ValidateInput<T>(variableId);
            if (_variables.ContainsKey(variableId))
                throw new InvalidOperationException($"Variable '{variableId}' is already declared.");
            var variable = new EcaVariable(new EcaVariableDefinition(variableId, typeof(T), defaultValue));
            _variables.Add(variableId, variable);
            return variable;
        }

        public bool Contains(string variableId)
        {
            ValidateId(variableId);
            return _variables.ContainsKey(variableId);
        }

        public T GetValue<T>(string variableId) => (T)Resolve<T>(variableId).CurrentValue;

        public bool TryGetValue<T>(string variableId, out T value)
        {
            ValidateInput<T>(variableId);
            value = default;
            if (!_variables.TryGetValue(variableId, out var variable)) return false;
            ValidateType<T>(variable);
            value = (T)variable.CurrentValue;
            return true;
        }

        public void SetValue<T>(string variableId, T value)
        {
            var variable = Resolve<T>(variableId);
            if (EqualityComparer<T>.Default.Equals((T)variable.CurrentValue, value)) return;
            SetTracked(variable, value);
        }

        public void ForceSetValue<T>(string variableId, T value) => SetTracked(Resolve<T>(variableId), value);

        public void SetCurrentValue<T>(string variableId, T value) => Resolve<T>(variableId).CurrentValue = value;

        private void SetTracked(EcaVariable variable, object value)
        {
            variable.OldValue = variable.CurrentValue;
            variable.CurrentValue = value;
            // Capture before invoking callbacks: reentrant writes cannot change this notification.
            var changed = new EcaVariableChanged(variable.Definition, variable.CurrentValue, variable.OldValue);
            VariableChanged?.Invoke(changed);
        }

        private EcaVariable Resolve<T>(string variableId)
        {
            ValidateInput<T>(variableId);
            if (!_variables.TryGetValue(variableId, out var variable))
                throw new InvalidOperationException($"Variable '{variableId}' is not declared.");
            ValidateType<T>(variable);
            return variable;
        }

        private static void ValidateInput<T>(string variableId)
        {
            ValidateId(variableId);
            var type = typeof(T);
            if (type != typeof(int) && type != typeof(float) && type != typeof(bool) && type != typeof(string))
                throw new NotSupportedException($"Variable type '{type}' is not supported.");
        }

        private static void ValidateId(string variableId)
        {
            if (string.IsNullOrWhiteSpace(variableId))
                throw new ArgumentException("Variable id cannot be empty.", nameof(variableId));
        }

        private static void ValidateType<T>(EcaVariable variable)
        {
            if (variable.Definition.ValueType != typeof(T))
                throw new InvalidOperationException($"Variable '{variable.Definition.Id}' declares '{variable.Definition.ValueType}', not '{typeof(T)}'.");
        }
    }
}
