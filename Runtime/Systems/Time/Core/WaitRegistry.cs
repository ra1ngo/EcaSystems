using System.Collections.Generic;

namespace EcaSystems.Time
{
    internal sealed class WaitRegistry
    {
        // Frozen membership, retaining the original entry references.
        internal IReadOnlyList<WaitTimer> GetSnapshot() => new List<WaitTimer>(_waits).AsReadOnly();

        private readonly HashSet<WaitTimer> _waits = new();
        internal HashSet<WaitTimer> Values => _waits;
        internal long LastId { get; private set; }
        internal bool HasPending => _waits.Count != 0;
        internal WaitTimer Create(double endTime, TimerScaleMode mode)
        {
            var wait = new WaitTimer(++LastId, endTime, mode);
            _waits.Add(wait);
            return wait;
        }
        internal bool Remove(WaitTimer wait) => _waits.Remove(wait);
    }
}
