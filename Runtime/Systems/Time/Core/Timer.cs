using System;

namespace EcaSystems.Time
{
    public sealed class Timer
    {
        private readonly Func<TimerScaleMode, double> _now;
        private readonly TimerRunner _runner;
        private double _endTime;
        private double _elapsed;
        private bool _completedZeroDuration;

        public string Id { get; }
        public double Duration { get; }
        public double Elapsed => State == TimerState.Running ? ElapsedAt(_now(ScaleMode)) : _elapsed;
        public double Remaining => Duration - Elapsed;
        public double Progress => Duration == 0 ? (_completedZeroDuration ? 1 : 0) : Elapsed / Duration;
        public TimerScaleMode ScaleMode { get; }
        public TimerState State { get; private set; }

        public event Action<Timer> Started;
        public event Action<Timer> Stopped;
        public event Action<Timer> Paused;
        public event Action<Timer> Resumed;
        public event Action<Timer> Completed;
        public event Action<Timer> Destroyed;

        internal long Version { get; private set; }

        internal Timer(string id, double duration, TimerScaleMode scaleMode, Func<TimerScaleMode, double> now, TimerRunner runner)
        {
            Id = id;
            Duration = duration;
            ScaleMode = scaleMode;
            _now = now;
            _runner = runner;
        }

        internal void Start()
        {
            if (State != TimerState.Stopped && State != TimerState.Completed) throw InvalidTransition("Start");
            _elapsed = 0;
            _completedZeroDuration = false;
            _endTime = _now(ScaleMode) + Duration;
            State = TimerState.Running;
            Version++;
            _runner.Add(this);
            Started?.Invoke(this);
        }

        internal void Pause()
        {
            if (State != TimerState.Running) throw InvalidTransition("Pause");
            _elapsed = Elapsed;
            State = TimerState.Paused;
            Version++;
            _runner.Remove(this);
            Paused?.Invoke(this);
        }

        internal void Resume()
        {
            if (State != TimerState.Paused) throw InvalidTransition("Resume");
            _endTime = _now(ScaleMode) + (Duration - _elapsed);
            State = TimerState.Running;
            Version++;
            _runner.Add(this);
            Resumed?.Invoke(this);
        }

        internal void Stop()
        {
            if (State != TimerState.Running && State != TimerState.Paused && State != TimerState.Completed)
                throw InvalidTransition("Stop");
            _elapsed = 0;
            _completedZeroDuration = false;
            State = TimerState.Stopped;
            Version++;
            _runner.Remove(this);
            Stopped?.Invoke(this);
        }

        internal void Destroy()
        {
            if (State == TimerState.Destroyed) throw InvalidTransition("Destroy");
            _elapsed = Elapsed;
            State = TimerState.Destroyed;
            Version++;
            _runner.Remove(this);
            Destroyed?.Invoke(this);
        }

        internal void Tick(double now)
        {
            if (State != TimerState.Running || now < _endTime) return;
            _elapsed = Duration;
            _completedZeroDuration = Duration == 0;
            State = TimerState.Completed;
            Version++;
            // Remove before callbacks: a callback may restart this exact timer.
            _runner.Remove(this);
            Completed?.Invoke(this);
        }

        private double ElapsedAt(double now) => Math.Max(0, Math.Min(Duration, Duration - (_endTime - now)));
        private InvalidOperationException InvalidTransition(string operation) =>
            new($"Cannot {operation} timer '{Id}' in state {State}.");
    }
}
