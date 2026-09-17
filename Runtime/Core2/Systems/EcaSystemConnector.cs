using System;
using System.Collections.Generic;

namespace EcaSystems.Core2
{
    public sealed class EcaSystemConnector
    {
        private readonly EcaSystemRegistry _systems;
        private readonly EcaSystemNamespaceRegistry _namespaces;
        private readonly IEcaEventRegistry _events;
        private readonly EcaCommandRegistry _commands;

        public EcaSystemConnector(EcaSystemRegistry systems, EcaSystemNamespaceRegistry namespaces,
            IEcaEventRegistry events, EcaCommandRegistry commands)
        {
            _systems = systems ?? throw new ArgumentNullException(nameof(systems));
            _namespaces = namespaces ?? throw new ArgumentNullException(nameof(namespaces));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        public void Attach(EcaSystem system)
        {
            if (system == null) throw new ArgumentNullException(nameof(system));
            RequirePresence(_systems.Contains(system.Id), false, "System", system.Id);
            ValidateExports(system, false);

            var undo = new Stack<Action>();
            try
            {
                _namespaces.Register(system.Namespace);
                undo.Push(() => RequireRemoved(_namespaces.Unregister(system.Namespace.Id)));
                foreach (var ecaEvent in system.Events)
                {
                    _events.Register(ecaEvent);
                    undo.Push(() => RequireRemoved(_events.Unregister(ecaEvent.Id)));
                }
                foreach (var command in system.Commands)
                {
                    _commands.Register(command);
                    undo.Push(() => RequireRemoved(_commands.Unregister(command.Id)));
                }
                _systems.Register(system);
            }
            catch (Exception failure)
            {
                Rollback(undo, failure);
                throw;
            }
        }

        public void Detach(EcaSystem system)
        {
            if (system == null) throw new ArgumentNullException(nameof(system));
            if (!ReferenceEquals(_systems.Resolve(system.Id), system))
                throw new InvalidOperationException($"System '{system.Id}' is registered with a different instance.");
            ValidateExports(system, true);

            var undo = new Stack<Action>();
            try
            {
                RequireRemoved(_systems.Unregister(system.Id));
                undo.Push(() => _systems.Register(system));
                foreach (var command in system.Commands)
                {
                    RequireRemoved(_commands.Unregister(command.Id));
                    undo.Push(() => _commands.Register(command));
                }
                foreach (var ecaEvent in system.Events)
                {
                    RequireRemoved(_events.Unregister(ecaEvent.Id));
                    undo.Push(() => _events.Register(ecaEvent));
                }
                RequireRemoved(_namespaces.Unregister(system.Namespace.Id));
            }
            catch (Exception failure)
            {
                Rollback(undo, failure);
                throw;
            }
        }

        private void ValidateExports(EcaSystem system, bool attached)
        {
            if (system.Namespace == null) throw new ArgumentException("System namespace is required.", nameof(system));
            ValidateId(system.Namespace.Id);
            RequirePresence(_namespaces.Contains(system.Namespace.Id), attached, "Namespace", system.Namespace.Id);
            var eventIds = attached ? null : new HashSet<string>(StringComparer.Ordinal);
            foreach (var ecaEvent in system.Events)
            {
                if (ecaEvent == null) throw new ArgumentException("System event cannot be null.", nameof(system));
                ValidateId(ecaEvent.Id);
                if (eventIds != null && !eventIds.Add(ecaEvent.Id))
                    throw new ArgumentException($"Duplicate event '{ecaEvent.Id}' in System.", nameof(system));
                RequirePresence(_events.Contains(ecaEvent.Id), attached, "Event", ecaEvent.Id);
            }
            var commandIds = attached ? null : new HashSet<string>(StringComparer.Ordinal);
            foreach (var command in system.Commands)
            {
                if (command == null) throw new ArgumentException("System command cannot be null.", nameof(system));
                ValidateId(command.Id);
                if (commandIds != null && !commandIds.Add(command.Id))
                    throw new ArgumentException($"Duplicate command '{command.Id}' in System.", nameof(system));
                RequirePresence(_commands.Contains(command.Id), attached, "Command", command.Id);
            }
        }

        private static void ValidateId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Export/namespace id cannot be empty.", nameof(id));
        }

        private static void RequirePresence(bool present, bool expected, string kind, string id)
        {
            if (present != expected)
                throw new InvalidOperationException($"{kind} '{id}' is {(present ? "already" : "not")} registered.");
        }

        private static void RequireRemoved(bool removed)
        {
            if (!removed) throw new InvalidOperationException("Expected registration could not be removed.");
        }

        // Local compensation for completed operations, not a snapshot or ownership graph.
        // Registries/configuration must not be mutated concurrently or from custom callbacks.
        private static void Rollback(Stack<Action> undo, Exception failure)
        {
            List<Exception> failures = null;
            while (undo.Count > 0)
            {
                try { undo.Pop()(); }
                catch (Exception rollbackFailure)
                {
                    failures ??= new List<Exception> { failure };
                    failures.Add(rollbackFailure);
                }
            }
            if (failures != null)
                throw new AggregateException("System operation failed and rollback could not fully restore registries.", failures);
        }
    }
}
