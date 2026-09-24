using System;

namespace EcaSystems.Variables.Eca
{
    public sealed class EcaVariableChangedEventState
    {
        public string StoreId { get; }
        public string VariableId { get; }
        public Type ValueType { get; }
        public object CurrentValue { get; }
        public object OldValue { get; }

        internal EcaVariableChangedEventState(string storeId, string variableId, Type valueType, object currentValue, object oldValue)
        {
            StoreId = storeId;
            VariableId = variableId;
            ValueType = valueType;
            CurrentValue = currentValue;
            OldValue = oldValue;
        }
    }
}
