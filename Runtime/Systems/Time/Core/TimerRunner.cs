using System;
using System.Collections.Generic;

namespace EcaSystems.Time
{
    internal sealed class TimerRunner
    {
        private readonly List<Timer> _active = new();
        private readonly Stack<List<(Timer timer, long version)>> _snapshots = new();
        private readonly Action<TimerRunner, bool> _setActive;

        internal TimerRunner(Action<TimerRunner, bool> setActive) => _setActive = setActive;

        internal void Add(Timer timer)
        {
            _active.Add(timer);
            if (_active.Count == 1) _setActive(this, true);
        }

        internal void Remove(Timer timer)
        {
            if (_active.Remove(timer) && _active.Count == 0) _setActive(this, false);
        }

        internal void Tick(double nowScaled, double nowUnscaled)
        {
            // Capture run identity as well as object identity. A prior callback may
            // stop/restart a later timer, which must wait for the next tick.
            // An in-flight snapshot is not pooled until iteration finishes, so even
            // a nested Tick gets its own buffer. Capacity is retained after warm-up.
            var snapshot = _snapshots.Count == 0 ? new List<(Timer timer, long version)>() : _snapshots.Pop();
            try
            {
                foreach (var timer in _active) snapshot.Add((timer, timer.Version));
                List<Exception> errors = null;
                foreach (var entry in snapshot)
                {
                    if (entry.timer.Version != entry.version) continue;
                    try
                    {
                        entry.timer.Tick(entry.timer.ScaleMode == TimerScaleMode.Scaled ? nowScaled : nowUnscaled);
                    }
                    catch (Exception error)
                    {
                        (errors ??= new List<Exception>()).Add(error);
                    }
                }
                if (errors != null) throw new AggregateException("Timer completion callback failed.", errors);
            }
            finally
            {
                snapshot.Clear();
                _snapshots.Push(snapshot);
            }
        }
    }
}
