using System;

namespace EcaSystems.Core2
{
    public sealed class EcaSystem
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public EcaSystemNamespace Namespace { get; }
        public IEcaEventRegistry Events { get; }
        public EcaCommandRegistry Commands { get; }
        public EcaStateRegistry States { get; }
        public IEcaSystemLifecycleConnector LifecycleConnector { get; }

        public EcaSystem(string id, EcaSystemNamespace systemNamespace,
            IEcaEventRegistry events, EcaCommandRegistry commands, EcaStateRegistry states,
            string name = null, string description = null, IEcaSystemLifecycleConnector lifecycleConnector = null)
        {
            Id = id;
            Namespace = systemNamespace ?? throw new ArgumentNullException(nameof(systemNamespace));
            // Local exports and metadata must remain stable while connected.
            Events = events ?? throw new ArgumentNullException(nameof(events));
            Commands = commands ?? throw new ArgumentNullException(nameof(commands));
            States = states ?? throw new ArgumentNullException(nameof(states));
            LifecycleConnector = lifecycleConnector;
            Name = name;
            Description = description;
        }
    }
}
