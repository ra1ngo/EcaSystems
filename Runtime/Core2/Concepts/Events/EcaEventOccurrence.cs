using System;

namespace EcaSystems.Core2
{
    public abstract class EcaEventOccurrence
    {
        public IEcaEvent Event { get; }

        protected EcaEventOccurrence(IEcaEvent ecaEvent)
        {
            Event = ecaEvent ?? throw new ArgumentNullException(nameof(ecaEvent));
        }

        public abstract void Accept(IEcaEventHandler handler);
    }

    internal sealed class EcaEventOccurrence<E> : EcaEventOccurrence
    {
        private readonly IEcaEvent<E> _event;
        private readonly E _eventState;

        internal EcaEventOccurrence(IEcaEvent<E> ecaEvent, E eventState) : base(ecaEvent)
        {
            _event = ecaEvent;
            _eventState = eventState;
        }

        public override void Accept(IEcaEventHandler handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            handler.Handle(_event, _eventState);
        }
    }
}
