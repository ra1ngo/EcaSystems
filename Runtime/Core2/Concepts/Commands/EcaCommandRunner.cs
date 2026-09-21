using System;
using System.Threading.Tasks;

namespace EcaSystems.Core2
{
    public sealed class EcaCommandRunner : IEcaCommands
    {
        private readonly EcaCommandRegistry _registry;

        public EcaCommandRunner(EcaCommandRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public Task Run<R, A>(string commandId, R state, IEcaActionContext context, A args)
            where R : IEcaRuleState
        {
            var command = _registry.Resolve(commandId);
            if (state is null) throw new ArgumentNullException(nameof(state));
            if (!command.RuleStateType.IsInstanceOfType(state))
                throw new InvalidOperationException(
                    $"Command '{commandId}' requires rule state '{command.RuleStateType}', received '{state.GetType()}'.");
            if (context != null && !command.ContextType.IsInstanceOfType(context))
                throw new InvalidOperationException(
                    $"Command '{commandId}' requires action context '{command.ContextType}', received '{context.GetType()}'.");

            // Declared args type remains significant even for null or a more specific runtime value.
            if (!command.ArgsType.IsAssignableFrom(typeof(A)) ||
                (args is null && command.ArgsType.IsValueType && Nullable.GetUnderlyingType(command.ArgsType) == null))
                throw new ArgumentException(
                    $"Command '{commandId}' requires arguments '{command.ArgsType}', received '{typeof(A)}'.", nameof(args));

            return command.Run(state, context, args)
                ?? throw new InvalidOperationException($"Command '{commandId}' returned null Task.");
        }
    }
}
