using System;

namespace EcaSystems.Time
{
    internal sealed class TimerController
    {
        private readonly TimerRegistry _registry;
        private readonly Action<TimerState, Timer, bool> _changed;
        internal TimerController(TimerRegistry registry, Action<TimerState, Timer, bool> changed)
        {
            _registry = registry;
            _changed = changed;
        }

        internal Timer Create(TimerCreateOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            TimerRegistry.ValidateId(options.Id);
            ValidateTiming(options.Duration, options.ScaleMode);
            var timer = new Timer(options);
            _registry.Register(timer);
            return timer;
        }

        // Tech debt: receives both clocks and selects by ScaleMode.
        // A narrower clock dependency can be considered in a later iteration.
        internal void Start(string id, double scaled, double unscaled)
        {
            var timer = _registry.Get(id);
            if (timer.State != TimerState.Stopped && timer.State != TimerState.Completed) throw Invalid(timer, "Start");
            timer.Elapsed = timer.Progress = 0;
            timer.Runtime.EndTime = Now(timer, scaled, unscaled) + timer.Duration;
            Change(timer, TimerState.Running);
        }

        internal void Pause(string id, double scaled, double unscaled)
        {
            var timer = _registry.Get(id);
            if (!timer.IsActive) throw Invalid(timer, "Pause");
            UpdateValues(timer, Now(timer, scaled, unscaled));
            Change(timer, TimerState.Paused);
        }

        internal void Resume(string id, double scaled, double unscaled)
        {
            var timer = _registry.Get(id);
            if (timer.State != TimerState.Paused) throw Invalid(timer, "Resume");
            timer.Runtime.EndTime = Now(timer, scaled, unscaled) + timer.Remaining;
            Change(timer, TimerState.Running, true);
        }

        internal void Stop(string id)
        {
            var timer = _registry.Get(id);
            if (timer.State != TimerState.Running && timer.State != TimerState.Paused && timer.State != TimerState.Completed)
                throw Invalid(timer, "Stop");
            timer.Elapsed = timer.Progress = 0;
            Change(timer, TimerState.Stopped);
        }

        internal void Destroy(string id, double scaled, double unscaled)
        {
            var timer = _registry.Get(id);
            if (timer.IsActive) UpdateValues(timer, Now(timer, scaled, unscaled));
            _registry.Remove(id);
            Change(timer, TimerState.Destroyed);
        }

        internal void Complete(Timer timer)
        {
            timer.Elapsed = timer.Duration;
            timer.Progress = 1;
            Change(timer, TimerState.Completed);
        }

        private void Change(Timer timer, TimerState state, bool resumed = false)
        {
            timer.State = state;
            timer.Runtime.Version++;
            _changed(state, timer, resumed);
        }

        internal static void UpdateValues(Timer timer, double now)
        {
            timer.Elapsed = Math.Max(0, Math.Min(timer.Duration, timer.Duration - (timer.Runtime.EndTime - now)));
            timer.Progress = timer.Duration == 0 ? 0 : timer.Elapsed / timer.Duration;
        }

        internal static double Now(Timer timer, double scaled, double unscaled) =>
            timer.ScaleMode == TimerScaleMode.Scaled ? scaled : unscaled;

        internal static void ValidateTiming(double duration, TimerScaleMode mode)
        {
            if (double.IsNaN(duration) || double.IsInfinity(duration) || duration < 0)
                throw new ArgumentOutOfRangeException(nameof(duration));
            if (mode != TimerScaleMode.Scaled && mode != TimerScaleMode.Unscaled)
                throw new ArgumentOutOfRangeException(nameof(mode));
        }

        private static InvalidOperationException Invalid(Timer timer, string operation) =>
            new($"Cannot {operation} timer '{timer.Id}' in state {timer.State}.");
    }
}
