using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EcaSystems.Core2
{
    public sealed class EcaCommandRegistry
    {
        private readonly Dictionary<string, IEcaCommandEntry> _commands = new(StringComparer.Ordinal);

        public void Register<TContext, TArgs>(IEcaCommand<TContext, TArgs> command)
            where TContext : IEcaActionContext
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            var id = command.Id;
            ValidateId(id);
            if (_commands.ContainsKey(id))
                throw new InvalidOperationException($"Command '{id}' is already registered.");
            _commands.Add(id, new EcaCommandEntry<TContext, TArgs>(command));
        }

        public bool Unregister(string commandId)
        {
            ValidateId(commandId);
            return _commands.Remove(commandId);
        }

        internal IEcaCommandEntry Resolve(string commandId)
        {
            ValidateId(commandId);
            if (_commands.TryGetValue(commandId, out var command)) return command;
            throw new InvalidOperationException($"Command '{commandId}' is not registered.");
        }

        private static void ValidateId(string commandId)
        {
            if (string.IsNullOrWhiteSpace(commandId))
                throw new ArgumentException("Command id cannot be empty or whitespace.", nameof(commandId));
        }
    }

    internal interface IEcaCommandEntry
    {
        Type ContextType { get; }
        Type ArgsType { get; }
        Task Run(IEcaActionContext context, object args);
    }

    internal sealed class EcaCommandEntry<TContext, TArgs> : IEcaCommandEntry
        where TContext : IEcaActionContext
    {
        private readonly IEcaCommand<TContext, TArgs> _command;
        public Type ContextType => typeof(TContext);
        public Type ArgsType => typeof(TArgs);

        public EcaCommandEntry(IEcaCommand<TContext, TArgs> command) => _command = command;

        public Task Run(IEcaActionContext context, object args) => _command.Run((TContext)context, (TArgs)args);
    }
}
