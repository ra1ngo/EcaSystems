using System;
using System.Collections.Generic;

namespace EcaSystems.Core2
{
    public sealed class EcaSystem
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public EcaSystemNamespace Namespace { get; }
        public IReadOnlyCollection<IEcaEvent> Events { get; }
        public IReadOnlyCollection<AEcaCommand> Commands { get; }

        public EcaSystem(string id, EcaSystemNamespace systemNamespace,
            IReadOnlyCollection<IEcaEvent> events = null, IReadOnlyCollection<AEcaCommand> commands = null,
            string name = null, string description = null)
        {
            Id = id;
            Namespace = systemNamespace;
            // Configuration is immutable by convention; retain the caller's collections.
            Events = events ?? Array.Empty<IEcaEvent>();
            Commands = commands ?? Array.Empty<AEcaCommand>();
            Name = name;
            Description = description;
        }
    }
}
