using System;
using EcaSystems.Core2;
using static EcaSystems.Time.Eca.EcaTimeEventKey;

namespace EcaSystems.Time.Eca
{
    /// <summary>Local Time exports only; global attachment and connection remain caller-owned.</summary>
    public static class TimeEcaSetup
    {
        public static EcaSystem CreateSystem(TimeSystem time)
        {
            if (time == null) throw new ArgumentNullException(nameof(time));
            var events = new EcaBaseEventRegistry();
            events.Register(new EcaTimeEvent(ECA_EVENT_TIMER_STARTED_ID, "Timer started"));
            events.Register(new EcaTimeEvent(ECA_EVENT_TIMER_STOPPED_ID, "Timer stopped"));
            events.Register(new EcaTimeEvent(ECA_EVENT_TIMER_PAUSED_ID, "Timer paused"));
            events.Register(new EcaTimeEvent(ECA_EVENT_TIMER_RESUMED_ID, "Timer resumed"));
            events.Register(new EcaTimeEvent(ECA_EVENT_TIMER_COMPLETED_ID, "Timer completed"));
            events.Register(new EcaTimeEvent(ECA_EVENT_TIMER_DESTROYED_ID, "Timer destroyed"));
            var commands = new EcaCommandRegistry();
            commands.Register(new EcaCreateTimerCommand(time));
            commands.Register(new EcaStartTimerCommand(time));
            commands.Register(new EcaStopTimerCommand(time));
            commands.Register(new EcaPauseTimerCommand(time));
            commands.Register(new EcaResumeTimerCommand(time));
            commands.Register(new EcaDestroyTimerCommand(time));
            commands.Register(new EcaWaitCommand(time));
            return new EcaSystem("time", new EcaSystemNamespace("time"), events, commands, new EcaStateRegistry(),
                name: "Time", description: "Standalone TimeSystem lifecycle and waits");
        }
    }
}
