namespace EcaSystems.Variables
{
    public sealed class EcaVariable
    {
        public EcaVariableDefinition Definition { get; }
        public object CurrentValue { get; internal set; }
        public object OldValue { get; internal set; }

        internal EcaVariable(EcaVariableDefinition definition)
        {
            Definition = definition;
            CurrentValue = definition.DefaultValue;
            OldValue = definition.DefaultValue;
        }
    }
}
