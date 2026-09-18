using UnityEngine;

namespace EcaSystems.Time
{
    internal sealed class WaitTimer
    {
        internal readonly long Id;
        internal readonly double EndTime;
        internal readonly TimerScaleMode ScaleMode;
        internal readonly AwaitableCompletionSource Completion = new();
        internal WaitTimer(long id, double endTime, TimerScaleMode mode)
        {
            Id = id;
            EndTime = endTime;
            ScaleMode = mode;
        }
    }
}
