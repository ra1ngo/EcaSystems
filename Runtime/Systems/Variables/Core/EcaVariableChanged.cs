namespace EcaSystems.Variables
{
    public sealed class EcaVariableChanged
    {
        public EcaVariableDefinition Definition { get; }
        public object CurrentValue { get; }
        public object OldValue { get; }

        internal EcaVariableChanged(EcaVariableDefinition definition, object currentValue, object oldValue)
        {
            Definition = definition;
            CurrentValue = currentValue;
            OldValue = oldValue;
        }
    }
}
