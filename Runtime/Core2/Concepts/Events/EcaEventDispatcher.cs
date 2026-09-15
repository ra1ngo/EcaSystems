using System;

namespace EcaSystems.Core2
{
    public sealed class EcaEventDispatcher : IEcaEventOccurrenceSource
    {
        private readonly EcaBaseEventRegistry _events;
        private Action<EcaEventOccurrence> _fired;

        public EcaEventDispatcher(EcaBaseEventRegistry events)
        {
            _events = events ?? throw new ArgumentNullException(nameof(events));
        }

        event Action<EcaEventOccurrence> IEcaEventOccurrenceSource.Fired
        {
            add => _fired += value;
            remove => _fired -= value;
        }

        public void Fire<E>(IEcaEvent<E> ecaEvent, E eventState)
        {
            if (ecaEvent == null) throw new ArgumentNullException(nameof(ecaEvent));
            if (!_events.CheckRegistered(ecaEvent))
                throw new InvalidOperationException($"Event '{ecaEvent.Id}' is not registered.");
            if (ecaEvent.EventStateType != typeof(E))
                throw new ArgumentException("Event metadata disagrees with its generic contract.", nameof(ecaEvent));

            _fired?.Invoke(new EcaEventOccurrence<E>(ecaEvent, eventState));
        }
    }
}
