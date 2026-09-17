using System;
using System.Collections.Generic;

namespace EcaSystems.Time
{
    internal sealed class TimerRegistry
    {
        private readonly Dictionary<string, Timer> _timers = new(StringComparer.Ordinal);

        internal void Register(Timer timer)
        {
            if (_timers.ContainsKey(timer.Id)) throw new InvalidOperationException($"Timer '{timer.Id}' already exists.");
            _timers.Add(timer.Id, timer);
        }

        internal Timer Get(string id)
        {
            if (TryGet(id, out var timer)) return timer;
            throw new InvalidOperationException($"Timer '{id}' is not registered.");
        }

        internal bool TryGet(string id, out Timer timer)
        {
            ValidateId(id);
            return _timers.TryGetValue(id, out timer);
        }

        internal Timer Remove(string id)
        {
            var timer = Get(id);
            _timers.Remove(id);
            return timer;
        }

        internal static void ValidateId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Timer id cannot be empty.", nameof(id));
        }
    }
}
