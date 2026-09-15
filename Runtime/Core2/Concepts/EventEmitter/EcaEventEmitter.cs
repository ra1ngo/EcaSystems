using System;

namespace EcaSystems.Core2
{
    public sealed class EcaEventEmitter : IEcaEventEmitter
    {
        private readonly IEcaEventRegistry _events;
        private IEcaEventHandler _handler;

        public EcaEventEmitter(IEcaEventRegistry events)
        {
            _events = events ?? throw new ArgumentNullException(nameof(events));
        }

        internal void Bind(IEcaEventHandler handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (_handler != null) throw new InvalidOperationException("Emitter already has a handler.");
            _handler = handler;
        }

        public void Fire<E>(IEcaEvent<E> ecaEvent, E eventState)
        {
            if (_handler == null) throw new InvalidOperationException("Emitter has not been bound to a handler.");
            if (ecaEvent == null) throw new ArgumentNullException(nameof(ecaEvent));
            if (!_events.CheckRegistered(ecaEvent))
                throw new InvalidOperationException($"Event '{ecaEvent.Id}' is not registered.");
            if (ecaEvent.EventStateType != typeof(E))
                throw new ArgumentException("Event metadata disagrees with its generic contract.", nameof(ecaEvent));

            _handler.Handle(ecaEvent, eventState);
        }
    }
}
