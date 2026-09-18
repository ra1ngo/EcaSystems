namespace EcaSystems.Time
{
    /// <summary>Observable timer data. Lifecycle is controlled through TimeSystem.</summary>
    public sealed class Timer
    {
        public string Id { get; }
        public double Duration { get; }
        public double Elapsed { get; internal set; }
        public double Remaining => Duration - Elapsed;
        public double Progress { get; internal set; }
        public TimerScaleMode ScaleMode { get; }
        public TimerState State { get; internal set; }
        public bool IsActive => State == TimerState.Running;
        internal TimerRuntime Runtime { get; } = new();

        internal Timer(TimerCreateOptions options)
        {
            Id = options.Id;
            Duration = options.Duration;
            ScaleMode = options.ScaleMode;
        }

        internal sealed class TimerRuntime
        {
            internal double EndTime;
            internal long Version;
        }
    }
}
