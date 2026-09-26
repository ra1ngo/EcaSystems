using System;
using System.Collections.Generic;

namespace EcaSystems.Core2
{
    public sealed class EcaCommandRegistry
    {
        public IReadOnlyList<AEcaCommand> GetSnapshot() => new List<AEcaCommand>(_commands.Values).AsReadOnly();

        private readonly Dictionary<string, AEcaCommand> _commands = new(StringComparer.Ordinal);
        public IReadOnlyCollection<AEcaCommand> Commands => _commands.Values;

        public void Register(AEcaCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            ValidateId(command.Id);
            if (_commands.ContainsKey(command.Id)) throw new InvalidOperationException($"command '{command.Id}' is already registered.");
            _commands.Add(command.Id, command);
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

        public AEcaCommand Resolve(string commandId)
        {
            ValidateId(commandId);
            if (_commands.TryGetValue(commandId, out var item)) return item;
            throw new InvalidOperationException($"command '{commandId}' is not registered.");
        }

        public bool CheckRegistered(AEcaCommand command) =>
            command != null && command.Id != null &&
            _commands.TryGetValue(command.Id, out var registered) && ReferenceEquals(registered, command);

        private static void ValidateId(string commandId)
        {
            if (string.IsNullOrWhiteSpace(commandId)) throw new ArgumentException("command id cannot be empty or whitespace.", nameof(commandId));
        }
    }
}
