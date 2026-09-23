using System;

namespace EcaSystems.Variables
{
    public sealed class EcaVariableDefinition
    {
        public string Id { get; }
        public Type ValueType { get; }
        public object DefaultValue { get; }

        internal EcaVariableDefinition(string id, Type valueType, object defaultValue)
        {
            Id = id;
            ValueType = valueType;
            DefaultValue = defaultValue;
        }
    }
}
