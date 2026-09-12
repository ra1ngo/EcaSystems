using System;

namespace EcaSystems.Core1
{
    public class EcaEvent<TEventState> : IEcaEvent<TEventState>
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public Type EventStateType => typeof(TEventState);

        public EcaEvent(string id, string name, string description = "")
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Event id cannot be empty.", nameof(id));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Event name cannot be empty.", nameof(name));
            Id = id;
            Name = name;
            Description = description ?? string.Empty;
        }
    }

    public sealed class EcaEvent : EcaEvent<EcaEventStateEmpty>
    {
        public EcaEvent(string id, string name, string description = "") : base(id, name, description) { }
    }
}
