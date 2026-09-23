namespace EcaSystems.Variables
{
    internal sealed class EcaVariableSnapshotMapper
    {
        public EcaVariableSnapshot Map(EcaVariable variable) =>
            new(variable.StoreId, variable.Definition, variable.CurrentValue, variable.OldValue);
    }
}
