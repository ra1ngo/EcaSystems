using System;

namespace EcaSystems.Core1
{
    public sealed class EcaExecutionMode
    {
        public EcaOverlap Overlap { get; }
        public int Limit { get; }

        public EcaExecutionMode(EcaOverlap overlap, int limit = -1)
        {
            if (limit < -1) throw new ArgumentOutOfRangeException(nameof(limit));
            if (overlap != EcaOverlap.Ignore && overlap != EcaOverlap.Allow)
                throw new ArgumentOutOfRangeException(nameof(overlap));
            Overlap = overlap;
            Limit = limit;
        }
    }
}
