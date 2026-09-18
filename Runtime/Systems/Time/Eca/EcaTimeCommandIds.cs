using System;

namespace EcaSystems.Time.Eca
{
    internal static class EcaTimeCommandIds
    {
        internal static string Get(EcaTimeCommandKey key) => key switch
        {
            EcaTimeCommandKey.ECA_COMMAND_TIMER_CREATE_ID => "time.timer.create",
            EcaTimeCommandKey.ECA_COMMAND_TIMER_START_ID => "time.timer.start",
            EcaTimeCommandKey.ECA_COMMAND_TIMER_STOP_ID => "time.timer.stop",
            EcaTimeCommandKey.ECA_COMMAND_TIMER_PAUSE_ID => "time.timer.pause",
            EcaTimeCommandKey.ECA_COMMAND_TIMER_RESUME_ID => "time.timer.resume",
            EcaTimeCommandKey.ECA_COMMAND_TIMER_DESTROY_ID => "time.timer.destroy",
            EcaTimeCommandKey.ECA_COMMAND_WAIT_ID => "time.wait",
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown Time Command key.")
        };
    }
}
