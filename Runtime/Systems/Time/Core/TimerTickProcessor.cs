using System;
using System.Collections.Generic;

namespace EcaSystems.Time
{
    internal sealed class TimerTickProcessor
    {
        private readonly TimerRegistry _registry;
        private readonly TimerController _controller;
        private readonly Stack<List<(Timer timer, long version)>> _buffers = new();
        internal TimerTickProcessor(TimerRegistry registry, TimerController controller)
        {
            _registry = registry;
            _controller = controller;
        }

        internal void Tick(double scaled, double unscaled)
        {
            var due = _buffers.Count == 0 ? new List<(Timer timer, long version)>() : _buffers.Pop();
            try
            {
                foreach (var timer in _registry.Values)
                {
                    if (!timer.IsActive) continue;
                    var now = TimerController.Now(timer, scaled, unscaled);
                    TimerController.UpdateValues(timer, now);
                    if (now >= timer.Runtime.EndTime) due.Add((timer, timer.Runtime.Version));
                }
                List<Exception> errors = null;
                foreach (var entry in due)
                {
                    if (!entry.timer.IsActive || entry.timer.Runtime.Version != entry.version) continue;
                    try { _controller.Complete(entry.timer); }
                    catch (Exception error) { (errors ??= new()).Add(error); }
                }
                if (errors != null) throw new AggregateException("Timer completion callback failed.", errors);
            }
            finally { due.Clear(); _buffers.Push(due); }
        }
    }
}
