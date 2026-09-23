using System;
using System.Collections.Generic;

namespace EcaSystems.Variables
{
    internal sealed class EcaVariableController
    {
        private readonly EcaVariable _variable;
        private readonly EcaVariableStore _owner;
        private readonly EcaVariableChangedMapper _changedMapper = new();

        internal EcaVariableController(EcaVariable variable, EcaVariableStore owner)
        {
            _variable = variable;
            _owner = owner;
        }

        internal T GetValue<T>()
        {
            ValidateType<T>();
            return (T)_variable.Data.CurrentValue;
        }

        internal void SetValue<T>(T value)
        {
            ValidateType<T>();
            if (EqualityComparer<T>.Default.Equals((T)_variable.Data.CurrentValue, value)) return;
            SetTracked(value);
        }

        internal void ForceSetValue<T>(T value)
        {
            ValidateType<T>();
            SetTracked(value);
        }

        internal void SetCurrentValue<T>(T value)
        {
            ValidateType<T>();
            _variable.Data.CurrentValue = value;
        }

        private void SetTracked(object value)
        {
            var data = _variable.Data;
            data.OldValue = data.CurrentValue;
            data.CurrentValue = value;
            // Map before callbacks, so nested mutations cannot alter this event.
            _owner.BubbleVariableChanged(_changedMapper.Map(_variable));
        }

        internal static void ValidateSupportedType<T>()
        {
            var type = typeof(T);
            if (type != typeof(int) && type != typeof(float) && type != typeof(bool) && type != typeof(string))
                throw new NotSupportedException($"Variable type '{type}' is not supported.");
        }

        private void ValidateType<T>()
        {
            ValidateSupportedType<T>();
            if (_variable.Definition.ValueType != typeof(T))
                throw new InvalidOperationException($"Variable '{_variable.Definition.Id}' declares '{_variable.Definition.ValueType}', not '{typeof(T)}'.");
        }
    }
}
