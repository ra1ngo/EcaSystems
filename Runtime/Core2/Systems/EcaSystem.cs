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

        public EcaSystem(string id, EcaSystemNamespace systemNamespace,
            IEcaEventRegistry events, EcaCommandRegistry commands,
            string name = null, string description = null)
        {
            Id = id;
            Namespace = systemNamespace ?? throw new ArgumentNullException(nameof(systemNamespace));
            // Local exports and metadata must remain stable while attached.
            Events = events ?? throw new ArgumentNullException(nameof(events));
            Commands = commands ?? throw new ArgumentNullException(nameof(commands));
            Name = name;
            Description = description;
        }
    }
}
