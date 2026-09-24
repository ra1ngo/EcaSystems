using System;

namespace EcaSystems.Variables.Eca
{
    public sealed class EcaSetVariableArgs
    {
        public string StoreId { get; }
        public string VariableId { get; }
        public object Value { get; }

        public EcaSetVariableArgs(string storeId, string variableId, object value)
        {
            if (string.IsNullOrWhiteSpace(storeId)) throw new ArgumentException("StoreId cannot be empty.", nameof(storeId));
            StoreId = storeId;
            if (string.IsNullOrWhiteSpace(variableId)) throw new ArgumentException("VariableId cannot be empty.", nameof(variableId));
            VariableId = variableId;
            Value = value;
        }
    }
}
