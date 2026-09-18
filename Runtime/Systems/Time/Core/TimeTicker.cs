using System;
using UnityEngine;

namespace EcaSystems.Time
{
    /// <summary>Main-thread clock and tick source shared by TimeSystem instances.</summary>
    public sealed class TimeTicker
    {
        public static TimeTicker Instance { get; } = new();
        public double CurrentTime { get; private set; }
        public double CurrentUnscaledTime { get; private set; }
        private Action<double, double> _tick;
        private Delegate[] _subscribers = Array.Empty<Delegate>();

        public event Action<double, double> TimeTick
        {
            add { _tick += value; _subscribers = _tick?.GetInvocationList() ?? Array.Empty<Delegate>(); }
            remove { _tick -= value; _subscribers = _tick?.GetInvocationList() ?? Array.Empty<Delegate>(); }
        }

        internal TimeTicker() { }
        internal int SubscriberCount => _subscribers.Length;

        internal void Reset()
        {
            _tick = null;
            _subscribers = Array.Empty<Delegate>();
            CurrentTime = CurrentUnscaledTime = 0;
        }

        internal void Publish(double scaled, double unscaled)
        {
            UpdateTime(scaled, unscaled);
            // Cached invocation snapshot changes only on subscribe/unsubscribe.
            // A callback may alter subscriptions without modifying this frame's snapshot.
            var subscribers = _subscribers;
            foreach (var subscriber in subscribers)
            {
                try { ((Action<double, double>)subscriber)(scaled, unscaled); }
                catch (Exception error) { Debug.LogException(error); }
            }
        }

        internal void UpdateTime(double scaled, double unscaled)
        {
            CurrentTime = scaled;
            CurrentUnscaledTime = unscaled;
        }
    }
}
