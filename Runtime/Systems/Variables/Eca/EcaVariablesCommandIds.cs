using System;

namespace EcaSystems.Variables.Eca
{
    internal static class EcaVariablesCommandIds
    {
        internal static string Get(EcaVariablesCommandKey key) => key switch
        {
            EcaVariablesCommandKey.ECA_COMMAND_RULE_VARIABLE_SET_ID => "variables.rule.variable.set",
            EcaVariablesCommandKey.ECA_COMMAND_VARIABLE_SET_ID => "variables.variable.set",
            EcaVariablesCommandKey.ECA_COMMAND_VARIABLE_FORCE_SET_ID => "variables.variable.force-set",
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown Variables Command key.")
        };
    }
}
