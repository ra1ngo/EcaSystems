using System;

namespace EcaSystems.Core2
{
    internal sealed class EcaEventEmitter : IEcaEventEmitter
    {
        private readonly IEcaEventRegistry _events;
        private readonly Action _checkCanFire;
        private IEcaEventHandler _handler;

        internal EcaEventEmitter(IEcaEventRegistry events, Action checkCanFire = null)
        {
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _checkCanFire = checkCanFire;
        }

        internal void Bind(IEcaEventHandler handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (_handler != null) throw new InvalidOperationException("Emitter already has a handler.");
            _handler = handler;
        }

        public void Fire<E>(IEcaEvent<E> ecaEvent, E eventState,
            IEcaConditionContext conditionContext = null, IEcaActionContext actionContext = null)
        {
            // A scoped emitter must reject its disposed owner before event validation,
            // exactly as a direct call to that Scope does.
            _checkCanFire?.Invoke();
            if (_handler == null) throw new InvalidOperationException("Emitter has not been bound to a handler.");
            if (ecaEvent == null) throw new ArgumentNullException(nameof(ecaEvent));
            if (!_events.CheckRegistered(ecaEvent))
                throw new InvalidOperationException($"Event '{ecaEvent.Id}' is not registered.");
            if (ecaEvent.EventStateType != typeof(E))
                throw new ArgumentException("Event metadata disagrees with its generic contract.", nameof(ecaEvent));

            _handler.Handle(ecaEvent, eventState, conditionContext, actionContext);
        }
    }
}
