namespace EcaSystems.Variables
{
    internal sealed class EcaVariableChangedMapper
    {
        public EcaVariableChanged Map(EcaVariable variable) =>
            new(variable.StoreId, variable.Definition, variable.CurrentValue, variable.OldValue);
    }
}
