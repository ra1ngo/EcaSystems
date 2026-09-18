using System;
using System.Collections.Generic;

namespace EcaSystems.Core2
{
    public sealed class EcaBaseEventRegistry : IEcaEventRegistry
    {
        private readonly Dictionary<string, IEcaEvent> _events = new(StringComparer.Ordinal);
        public IReadOnlyCollection<IEcaEvent> Events => _events.Values;

        public void Register(IEcaEvent ecaEvent)
        {
            if (ecaEvent == null) throw new ArgumentNullException(nameof(ecaEvent));
            ValidateId(ecaEvent.Id);
            if (_events.ContainsKey(ecaEvent.Id)) throw new InvalidOperationException($"event '{ecaEvent.Id}' is already registered.");
            _events.Add(ecaEvent.Id, ecaEvent);
        }

        public bool Unregister(string eventId)
        {
            ValidateId(eventId);
            return _events.Remove(eventId);
        }

        public bool Contains(string eventId)
        {
            ValidateId(eventId);
            return _events.ContainsKey(eventId);
        }

        public IEcaEvent Resolve(string eventId)
        {
            ValidateId(eventId);
            if (_events.TryGetValue(eventId, out var item)) return item;
            throw new InvalidOperationException($"event '{eventId}' is not registered.");
        }

        public bool CheckRegistered(IEcaEvent ecaEvent) =>
            ecaEvent != null && ecaEvent.Id != null &&
            _events.TryGetValue(ecaEvent.Id, out var registered) && ReferenceEquals(registered, ecaEvent);

        private static void ValidateId(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId)) throw new ArgumentException("event id cannot be empty or whitespace.", nameof(eventId));
        }
    }
}
