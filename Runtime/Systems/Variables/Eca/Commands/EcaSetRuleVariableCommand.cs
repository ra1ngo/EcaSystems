using System;
using System.Threading.Tasks;
using EcaSystems.Core2;

namespace EcaSystems.Variables.Eca
{
    public sealed class EcaSetRuleVariableCommand : AEcaCommand<IEcaScopeRuleState, IEcaActionContext, EcaSetRuleVariableArgs>
    {
        private readonly EcaVariablesLifecycleConnector _bindings;
        private readonly EcaSetVariableCommand _set;
        public override string Id => EcaVariablesCommandIds.Get(EcaVariablesCommandKey.ECA_COMMAND_RULE_VARIABLE_SET_ID);

        // Created with the same connector owned by the passive System descriptor.
        internal EcaSetRuleVariableCommand(EcaVariablesLifecycleConnector bindings, EcaVariablesSystem variables)
        {
            _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
            _set = new EcaSetVariableCommand(variables);
        }

        public override Task Run(IEcaScopeRuleState state, IEcaActionContext context, EcaSetRuleVariableArgs args)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));
            var store = _bindings.ResolveRuleStore(state);
            return _set.Run(state, context, new EcaSetVariableArgs(store.Id, args.VariableId, args.Value));
        }
    }
}
