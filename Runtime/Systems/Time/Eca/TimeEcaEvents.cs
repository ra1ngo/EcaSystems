using System;
using EcaSystems.Core2;

namespace EcaSystems.Time.Eca
{
    public sealed class TimeEcaEvents
    {
        public IEcaEvent<TimerEventState> TimerStarted { get; } = new Declaration("time.timer.started", "Timer started");
        public IEcaEvent<TimerEventState> TimerStopped { get; } = new Declaration("time.timer.stopped", "Timer stopped");
        public IEcaEvent<TimerEventState> TimerPaused { get; } = new Declaration("time.timer.paused", "Timer paused");
        public IEcaEvent<TimerEventState> TimerResumed { get; } = new Declaration("time.timer.resumed", "Timer resumed");
        public IEcaEvent<TimerEventState> TimerCompleted { get; } = new Declaration("time.timer.completed", "Timer completed");
        public IEcaEvent<TimerEventState> TimerDestroyed { get; } = new Declaration("time.timer.destroyed", "Timer destroyed");

        private sealed class Declaration : IEcaEvent<TimerEventState>
        {
            public string Id { get; }
            public string Name { get; }
            public string Description => Name;
            public Type EventStateType => typeof(TimerEventState);
            internal Declaration(string id, string name) { Id = id; Name = name; }
        }
    }
}
