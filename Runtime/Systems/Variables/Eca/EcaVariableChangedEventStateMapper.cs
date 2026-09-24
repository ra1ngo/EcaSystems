using System;

namespace EcaSystems.Variables.Eca
{
    internal sealed class EcaVariableChangedEventStateMapper
    {
        public EcaVariableChangedEventState Map(EcaVariableChanged change)
        {
            if (change == null) throw new ArgumentNullException(nameof(change));
            return new EcaVariableChangedEventState(change.StoreId, change.Definition.Id,
                change.Definition.ValueType, change.CurrentValue, change.OldValue);
        }
    }
}
