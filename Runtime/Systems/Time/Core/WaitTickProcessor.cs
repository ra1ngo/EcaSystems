using System;
using System.Collections.Generic;

namespace EcaSystems.Time
{
    internal sealed class WaitTickProcessor
    {
        private readonly WaitRegistry _registry;
        private readonly Stack<List<WaitTimer>> _buffers = new();
        internal WaitTickProcessor(WaitRegistry registry) => _registry = registry;
        internal void Tick(double scaled, double unscaled, long lastId)
        {
            var due = _buffers.Count == 0 ? new List<WaitTimer>() : _buffers.Pop();
            try
            {
                foreach (var wait in _registry.Values)
                    if (wait.Id <= lastId && (wait.ScaleMode == TimerScaleMode.Scaled ? scaled : unscaled) >= wait.EndTime)
                        due.Add(wait);
                List<Exception> errors = null;
                foreach (var wait in due)
                {
                    if (!_registry.Remove(wait)) continue;
                    try { wait.Completion.SetResult(); }
                    catch (Exception error) { (errors ??= new()).Add(error); }
                }
                if (errors != null) throw new AggregateException("Wait continuation failed.", errors);
            }
            finally { due.Clear(); _buffers.Push(due); }
        }
    }
}
