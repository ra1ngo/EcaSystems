namespace EcaSystems.Time.Eca
{
    public readonly struct TimeWaitArgs
    {
        public double Duration { get; }
        public TimerScaleMode ScaleMode { get; }
        public TimeWaitArgs(double duration, TimerScaleMode scaleMode = TimerScaleMode.Scaled)
        {
            Duration = duration;
            ScaleMode = scaleMode;
        }
    }
}
