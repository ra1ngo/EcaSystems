using System;

namespace EcaSystems.Core2
{
    internal sealed class EcaEventEmitter : IEcaEventEmitter
    {
        private IEcaEventHandler _handler;

        internal void Bind(IEcaEventHandler handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (_handler != null) throw new InvalidOperationException("Emitter already has a handler.");
            _handler = handler;
        }

        public void Fire<E>(IEcaEvent<E> ecaEvent, E eventState,
            IEcaConditionContext conditionContext = null, IEcaActionContext actionContext = null)
        {
            if (_handler == null) throw new InvalidOperationException("Emitter has not been bound to a handler.");

            _handler.Handle(ecaEvent, eventState, conditionContext, actionContext);
        }
    }
}
