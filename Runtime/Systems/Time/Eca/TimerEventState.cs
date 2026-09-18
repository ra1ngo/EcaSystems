namespace EcaSystems.Time.Eca
{
    public sealed class TimerEventState
    {
        public string TimerId { get; }
        public double Duration { get; }
        public double Elapsed { get; }
        public double Remaining { get; }
        public double Progress { get; }
        public TimerScaleMode ScaleMode { get; }
        public TimerState State { get; }

        internal TimerEventState(Timer timer)
        {
            TimerId = timer.Id;
            Duration = timer.Duration;
            Elapsed = timer.Elapsed;
            Remaining = timer.Remaining;
            Progress = timer.Progress;
            ScaleMode = timer.ScaleMode;
            State = timer.State;
        }
    }
}
