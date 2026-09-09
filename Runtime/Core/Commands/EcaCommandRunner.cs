using System;
using System.Threading.Tasks;

namespace EcaSystems.Core
{
    public sealed class EcaCommandRunner : IEcaCommandRunner
    {
        private readonly EcaCommandRegistry _registry;

        public EcaCommandRunner(EcaCommandRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public IEcaCommands Bind(IEcaActionContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            return new BoundCommands(_registry, context);
        }

        private sealed class BoundCommands : IEcaCommands
        {
            private readonly EcaCommandRegistry _registry;
            private readonly IEcaActionContext _context;

            public BoundCommands(EcaCommandRegistry registry, IEcaActionContext context)
            {
                _registry = registry;
                _context = context;
            }

            public Task Run<TArgs>(string commandId, TArgs args)
            {
                var command = _registry.Resolve(commandId);
                if (!command.ContextType.IsInstanceOfType(_context))
                    throw new InvalidOperationException($"Command '{commandId}' requires action context '{command.ContextType}', received '{_context.GetType()}'.");

                // Проверяем объявленный тип и null: несовместимый тип нельзя скрыть за null.
                if (!command.ArgsType.IsAssignableFrom(typeof(TArgs)) ||
                    (args is null && command.ArgsType.IsValueType && Nullable.GetUnderlyingType(command.ArgsType) == null))
                    throw new ArgumentException($"Command '{commandId}' requires arguments '{command.ArgsType}', received '{typeof(TArgs)}'.", nameof(args));

                return command.Run(_context, args)
                    ?? throw new InvalidOperationException($"Command '{commandId}' returned null Task.");
            }
        }
    }
}
