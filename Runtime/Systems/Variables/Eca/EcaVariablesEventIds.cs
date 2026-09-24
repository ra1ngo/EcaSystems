using System;

namespace EcaSystems.Variables.Eca
{
    internal static class EcaVariablesEventIds
    {
        internal static string Get(EcaVariablesEventKey key) => key switch
        {
            EcaVariablesEventKey.ECA_EVENT_VARIABLE_CHANGED_ID => "variables.variable.changed",
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown Variables Event key.")
        };
    }
}
