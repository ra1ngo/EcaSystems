using System;

namespace EcaSystems.Core
{
    public sealed class EcaEvent<TEventContext> : IEcaEvent
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }

        public Type EventContextType => typeof(TEventContext);

        public EcaEvent(string id, string name, string description = "")
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Event id cannot be empty.", nameof(id));

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Event name cannot be empty.", nameof(name));

            Id = id;
            Name = name;
            Description = description ?? string.Empty;
        }
    }
}