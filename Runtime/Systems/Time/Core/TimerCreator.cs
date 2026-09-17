using System;

namespace EcaSystems.Time
{
    internal sealed class TimerCreator
    {
        private readonly TimerRegistry _registry;
        private readonly TimerRunner _runner;
        private readonly Func<TimerScaleMode, double> _now;

        internal TimerCreator(TimerRegistry registry, TimerRunner runner, Func<TimerScaleMode, double> now)
        {
            _registry = registry;
            _runner = runner;
            _now = now;
        }

        internal Timer Create(TimerCreateOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            TimerRegistry.ValidateId(options.Id);
            ValidateTiming(options.Duration, options.ScaleMode);
            var timer = new Timer(options.Id, options.Duration, options.ScaleMode, _now, _runner);
            _registry.Register(timer);
            return timer;
        }

        internal static void ValidateTiming(double duration, TimerScaleMode scaleMode)
        {
            if (double.IsNaN(duration) || double.IsInfinity(duration) || duration < 0)
                throw new ArgumentOutOfRangeException(nameof(duration), "Duration must be finite and non-negative.");
            if (scaleMode != TimerScaleMode.Scaled && scaleMode != TimerScaleMode.Unscaled)
                throw new ArgumentOutOfRangeException(nameof(scaleMode));
        }
    }
}
