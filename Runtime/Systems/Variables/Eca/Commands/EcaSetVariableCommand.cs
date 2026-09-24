using System;
using System.Threading.Tasks;
using EcaSystems.Core2;

namespace EcaSystems.Variables.Eca
{
    public sealed class EcaSetVariableCommand : AEcaCommand<IEcaRuleState, IEcaActionContext, EcaSetVariableArgs>
    {
        private readonly EcaVariablesSystem _variables;
        public override string Id => EcaVariablesCommandIds.Get(EcaVariablesCommandKey.ECA_COMMAND_VARIABLE_SET_ID);

        public EcaSetVariableCommand(EcaVariablesSystem variables) =>
            _variables = variables ?? throw new ArgumentNullException(nameof(variables));

        public override Task Run(IEcaRuleState state, IEcaActionContext context, EcaSetVariableArgs args)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));
            var variable = _variables.GetStore(args.StoreId).GetVariable(args.VariableId);
            var type = variable.Definition.ValueType;
            var value = args.Value;
            if (type == typeof(int) && value is int number) variable.SetValue(number);
            else if (type == typeof(float) && value is float floating) variable.SetValue(floating);
            else if (type == typeof(bool) && value is bool boolean) variable.SetValue(boolean);
            else if (type == typeof(string) && (value == null || value is string)) variable.SetValue((string)value);
            else throw new ArgumentException($"Value must have the exact declared type '{type}'; null is allowed only for string.", nameof(args));
            return Task.CompletedTask;
        }
    }
}
