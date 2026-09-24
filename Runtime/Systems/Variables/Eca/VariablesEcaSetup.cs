using System;
using EcaSystems.Core2;

namespace EcaSystems.Variables.Eca
{
    /// <summary>Local exports only; global connection and adapter lifecycle remain caller-owned.</summary>
    public static class VariablesEcaSetup
    {
        public static EcaSystem CreateSystem(EcaVariablesSystem variables)
        {
            if (variables == null) throw new ArgumentNullException(nameof(variables));
            var events = new EcaBaseEventRegistry();
            events.Register(new EcaVariableChangedEvent());
            var commands = new EcaCommandRegistry();
            commands.Register(new EcaSetVariableCommand(variables));
            commands.Register(new EcaForceSetVariableCommand(variables));
            var states = new EcaStateRegistry();
            states.Register(EcaVariablesStateIds.Get(EcaVariablesStateKey.ECA_STATE_SYSTEM_ID),
                _ => variables.GetState());
            states.Register(EcaVariablesStateIds.Get(EcaVariablesStateKey.ECA_STATE_STORE_ID),
                (_, payload) => ResolveStore(variables, payload).GetStoreState());
            states.Register(EcaVariablesStateIds.Get(EcaVariablesStateKey.ECA_STATE_SUBTREE_ID),
                (_, payload) => ResolveStore(variables, payload).GetSubtreeState());
            return new EcaSystem("variables", new EcaSystemNamespace("variables"), events, commands, states,
                name: "Variables", description: "Standalone Variables state and mutations");
        }

        private static EcaVariableStore ResolveStore(EcaVariablesSystem variables, object payload)
        {
            if (payload is not EcaVariablesStatePayload selector)
                throw new ArgumentException("An EcaVariablesStatePayload is required.", nameof(payload));
            return variables.GetStore(selector.StoreId);
        }
    }
}
