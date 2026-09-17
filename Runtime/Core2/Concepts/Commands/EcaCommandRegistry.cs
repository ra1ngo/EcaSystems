using System;
using System.Collections.Generic;

namespace EcaSystems.Core2
{
    public sealed class EcaCommandRegistry
    {
        private readonly Dictionary<string, AEcaCommand> _commands = new(StringComparer.Ordinal);

        public void Register(AEcaCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            var id = command.Id;
            ValidateId(id);
            if (_commands.ContainsKey(id))
                throw new InvalidOperationException($"Command '{id}' is already registered.");
            _commands.Add(id, command);
        }

        public bool Unregister(string commandId)
        {
            ValidateId(commandId);
            return _commands.Remove(commandId);
        }

        public bool Contains(string commandId)
        {
            ValidateId(commandId);
            return _commands.ContainsKey(commandId);
        }

        internal AEcaCommand Resolve(string commandId)
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
}
