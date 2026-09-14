using System;
using System.Collections.Generic;

namespace EcaSystems.Core2
{
    public sealed class EcaBaseEventRegistry : IEcaEventRegistry
    {
        private readonly Dictionary<string, IEcaEvent> _events =
            new(StringComparer.Ordinal);

        public void Register<TEventState>(IEcaEvent<TEventState> ecaEvent)
        {
            if (ecaEvent == null) throw new ArgumentNullException(nameof(ecaEvent));
            if (string.IsNullOrWhiteSpace(ecaEvent.Id)) throw new ArgumentException("Event id cannot be empty.",nameof(ecaEvent));
            if (ecaEvent.EventStateType != typeof(TEventState))
                throw new ArgumentException("Event metadata disagrees with its generic contract.",nameof(ecaEvent));
            if (_events.TryGetValue(ecaEvent.Id, out var existing))
                throw new InvalidOperationException($"Event '{ecaEvent.Id}' already registered as '{existing.EventStateType}'; requested '{typeof(TEventState)}'.");

            _events.Add(ecaEvent.Id, ecaEvent);
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