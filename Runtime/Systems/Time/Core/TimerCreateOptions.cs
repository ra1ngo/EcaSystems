namespace EcaSystems.Time
{
    public sealed class TimerCreateOptions
    {
        public string Id { get; }
        public double Duration { get; }
        public TimerScaleMode ScaleMode { get; }

        public TimerCreateOptions(string id, double duration, TimerScaleMode scaleMode = TimerScaleMode.Scaled)
        {
            Id = id;
            Duration = duration;
            ScaleMode = scaleMode;
        }
    }
}
