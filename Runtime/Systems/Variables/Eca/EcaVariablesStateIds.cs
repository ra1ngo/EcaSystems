using System;

namespace EcaSystems.Variables.Eca
{
    internal static class EcaVariablesStateIds
    {
        internal static string Get(EcaVariablesStateKey key) => key switch
        {
            EcaVariablesStateKey.ECA_STATE_SYSTEM_ID => "variables.state",
            EcaVariablesStateKey.ECA_STATE_STORE_ID => "variables.store",
            EcaVariablesStateKey.ECA_STATE_SUBTREE_ID => "variables.subtree",
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown Variables State key.")
        };
    }
}
