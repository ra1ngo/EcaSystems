using System;

namespace EcaSystems.Time.Eca
{
    internal static class EcaTimeEventIds
    {
        internal static string Get(EcaTimeEventKey key) => key switch
        {
            EcaTimeEventKey.ECA_EVENT_TIMER_STARTED_ID => "time.timer.started",
            EcaTimeEventKey.ECA_EVENT_TIMER_STOPPED_ID => "time.timer.stopped",
            EcaTimeEventKey.ECA_EVENT_TIMER_PAUSED_ID => "time.timer.paused",
            EcaTimeEventKey.ECA_EVENT_TIMER_RESUMED_ID => "time.timer.resumed",
            EcaTimeEventKey.ECA_EVENT_TIMER_COMPLETED_ID => "time.timer.completed",
            EcaTimeEventKey.ECA_EVENT_TIMER_DESTROYED_ID => "time.timer.destroyed",
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown Time Event key.")
        };
    }
}
