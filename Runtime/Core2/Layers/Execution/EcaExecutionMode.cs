using System;

namespace EcaSystems.Core2
{
    public sealed class EcaExecutionMode
    {
        public EcaExecutionModeOverlap Overlap { get; }
        public int Limit { get; }

        public EcaExecutionMode(EcaExecutionModeOverlap overlap, int limit = -1)
        {
            if (overlap != EcaExecutionModeOverlap.Ignore && overlap != EcaExecutionModeOverlap.Allow)
                throw new ArgumentOutOfRangeException(nameof(overlap));
            if (limit < -1) throw new ArgumentOutOfRangeException(nameof(limit));
            Overlap = overlap;
            Limit = limit;
        }
    }
}
