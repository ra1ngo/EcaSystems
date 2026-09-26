using System;

namespace EcaSystems.Variables.Eca
{
    public sealed class EcaSetRuleVariableArgs
    {
        public string VariableId { get; }
        public object Value { get; }
        public EcaSetRuleVariableArgs(string variableId, object value)
        {
            if (string.IsNullOrWhiteSpace(variableId)) throw new ArgumentException("VariableId cannot be empty.", nameof(variableId));
            VariableId = variableId;
            Value = value;
        }
    }
}
