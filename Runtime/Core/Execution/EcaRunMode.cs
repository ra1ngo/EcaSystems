using System;

namespace EcaSystems.Core
{
    public sealed class EcaRunMode
    {
        public EcaOverlap Overlap { get; }
        public int Limit { get; }

        public EcaRunMode(EcaOverlap overlap, int limit = -1)
        {
            if (limit < -1) throw new ArgumentOutOfRangeException(nameof(limit));
            Overlap = overlap;
            Limit = limit;
        }
    }
}
