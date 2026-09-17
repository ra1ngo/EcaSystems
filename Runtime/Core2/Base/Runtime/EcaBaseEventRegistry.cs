using System;
using System.Collections.Generic;

namespace EcaSystems.Core2
{
    public sealed class EcaBaseEventRegistry : IEcaEventRegistry
    {
        private readonly Dictionary<string, IEcaEvent> _events = new(StringComparer.Ordinal);

        public void Register<E>(IEcaEvent<E> ecaEvent)
        {
            if (ecaEvent == null) throw new ArgumentNullException(nameof(ecaEvent));
            if (string.IsNullOrWhiteSpace(ecaEvent.Id)) throw new ArgumentException("Event id cannot be empty.", nameof(ecaEvent));
            if (ecaEvent.EventStateType != typeof(E))
                throw new ArgumentException("Event metadata disagrees with its generic contract.", nameof(ecaEvent));
            Register((IEcaEvent)ecaEvent);
        }

        public void Register(IEcaEvent ecaEvent)
        {
            if (ecaEvent == null) throw new ArgumentNullException(nameof(ecaEvent));
            ValidateId(ecaEvent.Id);
            if (_events.TryGetValue(ecaEvent.Id, out var existing))
                throw new InvalidOperationException(
                    $"Event '{ecaEvent.Id}' already registered as '{existing.EventStateType}'; requested '{ecaEvent.EventStateType}'.");

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

        private static void ValidateId(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId)) throw new ArgumentException("Event id cannot be empty.", nameof(eventId));
        }

        public bool CheckRegistered(IEcaEvent ecaEvent)
        {
            return ecaEvent != null
                && ecaEvent.Id != null
                && _events.TryGetValue(ecaEvent.Id, out var registered)
                && registered.EventStateType == ecaEvent.EventStateType;
        }
    }
}
