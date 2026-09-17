using System;
using UnityEngine;

namespace EcaSystems.Time
{
    /// <summary>Instance-owned timers. Use on Unity's main thread.</summary>
    public sealed class TimeSystem
    {
        private readonly TimerRegistry _registry = new();
        private readonly TimerCreator _creator;
        private readonly TimerRunner _runner;
        private readonly Func<TimerScaleMode, double> _now;

        public TimeSystem() : this(ReadUnityTime, TimeSystemPlayerLoop.SetActive) { }

        internal TimeSystem(Func<TimerScaleMode, double> now, Action<TimerRunner, bool> setActive)
        {
            _now = now;
            _runner = new TimerRunner(setActive);
            _creator = new TimerCreator(_registry, _runner, now);
        }

        public Timer CreateTimer(TimerCreateOptions options) => _creator.Create(options);
        public Timer Get(string timerId) => _registry.Get(timerId);
        public bool TryGet(string timerId, out Timer timer) => _registry.TryGet(timerId, out timer);
        public void Start(string timerId) => Get(timerId).Start();
        public void Stop(string timerId) => Get(timerId).Stop();
        public void Pause(string timerId) => Get(timerId).Pause();
        public void Resume(string timerId) => Get(timerId).Resume();
        public void DestroyTimer(string timerId) => _registry.Remove(timerId).Destroy();

        public Awaitable Wait(double duration, TimerScaleMode scaleMode = TimerScaleMode.Scaled)
        {
            TimerCreator.ValidateTiming(duration, scaleMode);
            var completion = new AwaitableCompletionSource();
            // Private timer uses the same progression but never enters the ID registry.
            var timer = new Timer(null, duration, scaleMode, _now, _runner);
            timer.Completed += _ => completion.SetResult();
            timer.Start();
            return completion.Awaitable;
        }

        internal void Tick(double nowScaled, double nowUnscaled) => _runner.Tick(nowScaled, nowUnscaled);

        private static double ReadUnityTime(TimerScaleMode mode) => mode == TimerScaleMode.Scaled
            ? UnityEngine.Time.timeAsDouble
            : UnityEngine.Time.unscaledTimeAsDouble;
    }
}
