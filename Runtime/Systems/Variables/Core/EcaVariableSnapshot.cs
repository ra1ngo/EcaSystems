namespace EcaSystems.Variables
{
    public sealed class EcaVariableSnapshot
    {
        public string StoreId { get; }
        public EcaVariableDefinition Definition { get; }
        public object CurrentValue { get; }
        public object OldValue { get; }

        internal EcaVariableSnapshot(string storeId, EcaVariableDefinition definition, object currentValue, object oldValue)
        {
            StoreId = storeId;
            Definition = definition;
            CurrentValue = currentValue;
            OldValue = oldValue;
        }
    }
}
