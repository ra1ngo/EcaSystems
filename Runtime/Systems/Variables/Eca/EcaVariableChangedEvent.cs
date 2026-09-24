using System;
using EcaSystems.Core2;

namespace EcaSystems.Variables.Eca
{
    internal sealed class EcaVariableChangedEvent : IEcaEvent<EcaVariableChangedEventState>
    {
        public string Id => EcaVariablesEventIds.Get(EcaVariablesEventKey.ECA_EVENT_VARIABLE_CHANGED_ID);
        public string Name => "Variable changed";
        public string Description => Name;
        public Type EventStateType => typeof(EcaVariableChangedEventState);
    }
}
