using System;

namespace EcaSystems.Core2
{
    public sealed class EcaEventReceiver : IDisposable
    {
        private readonly EcaBaseEventRegistry _events;
        private readonly IEcaEventOccurrenceSource _source;
        private IEcaEventHandler _handler;
        private bool _isDisposed;

        public EcaEventReceiver(EcaBaseEventRegistry events, IEcaEventOccurrenceSource source)
        {
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _source.Fired += OnFired;
        }

        public void Subscribe(IEcaEventHandler handler)
        {
            ThrowIfDisposed();
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (_handler != null) throw new InvalidOperationException("Receiver already has a handler.");
            _handler = handler;
        }

        public bool Unsubscribe(IEcaEventHandler handler)
        {
            ThrowIfDisposed();
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (!ReferenceEquals(_handler, handler)) return false;
            _handler = null;
            return true;
        }

        private void OnFired(EcaEventOccurrence occurrence)
        {
            if (_isDisposed) return;
            if (occurrence == null) throw new ArgumentNullException(nameof(occurrence));
            if (!_events.CheckRegistered(occurrence.Event))
                throw new InvalidOperationException($"Event '{occurrence.Event.Id}' is not registered.");

            if (_handler != null) occurrence.Accept(_handler);
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _source.Fired -= OnFired;
            _handler = null;
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(EcaEventReceiver));
        }
    }
}
