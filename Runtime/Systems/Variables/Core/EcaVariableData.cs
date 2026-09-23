namespace EcaSystems.Variables
{
    public sealed class EcaVariableData
    {
        public string StoreId { get; }
        public EcaVariableDefinition Definition { get; }
        public object CurrentValue { get; internal set; }
        public object OldValue { get; internal set; }

        internal EcaVariableData(string storeId, EcaVariableDefinition definition)
        {
            StoreId = storeId;
            Definition = definition;
            CurrentValue = definition.DefaultValue;
            OldValue = definition.DefaultValue;
        }
    }
}
