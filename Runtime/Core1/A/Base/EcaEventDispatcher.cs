using System;

namespace EcaSystems.Core1
{
    public sealed class EcaEventDispatcher
    {
        private readonly EcaEventRegistry _events;
        public event Action<EcaEventOccurrence> Fired;

        public EcaEventDispatcher(EcaEventRegistry events)
        {
            _events = events ?? throw new ArgumentNullException(nameof(events));
        }

        public void Fire(IEcaEvent<EcaEventStateEmpty> ecaEvent) => Fire(ecaEvent, default);

        public void Fire<TEventState>(IEcaEvent<TEventState> ecaEvent, TEventState eventState)
        {
            _events.RequireRegistered(ecaEvent);
            if (ecaEvent.EventStateType != typeof(TEventState))
                throw new ArgumentException("Event metadata disagrees with its generic contract.", nameof(ecaEvent));
            Fired?.Invoke(new EcaEventOccurrence<TEventState>(ecaEvent, eventState));
        }
    }
}
