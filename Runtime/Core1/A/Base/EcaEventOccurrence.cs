using System;

namespace EcaSystems.Core1
{
    /* Generic dispatch preserves the declared T, including value types. The visitor
       is public so another RuleRunner can reuse the same non-generic engine. */
    public interface IEcaEventOccurrenceVisitor<out TResult>
    {
        TResult Visit<TEventState>(IEcaEvent<TEventState> ecaEvent, TEventState eventState);
    }

    public abstract class EcaEventOccurrence
    {
        public IEcaEvent Event { get; }

        internal EcaEventOccurrence(IEcaEvent ecaEvent) { Event = ecaEvent; }

        public abstract TResult Accept<TResult>(IEcaEventOccurrenceVisitor<TResult> visitor);
    }

    internal sealed class EcaEventOccurrence<TEventState> : EcaEventOccurrence
    {
        private readonly IEcaEvent<TEventState> _event;
        private readonly TEventState _state;

        internal EcaEventOccurrence(IEcaEvent<TEventState> ecaEvent, TEventState state) : base(ecaEvent)
        {
            _event = ecaEvent;
            _state = state;
        }

        public override TResult Accept<TResult>(IEcaEventOccurrenceVisitor<TResult> visitor)
        {
            if (visitor == null) throw new ArgumentNullException(nameof(visitor));
            return visitor.Visit(_event, _state);
        }
    }
}
