using System;
using UnityEngine;

namespace EcaSystems.Time
{
    /// <summary>Instance-owned timers. Use on Unity's main thread.</summary>
    public sealed class TimeSystem
    {
        private readonly TimeTicker _ticker;
        private readonly TimerRegistry _registry = new();
        private readonly WaitRegistry _waits = new();
        private readonly TimerController _controller;
        private readonly TimerTickProcessor _timers;
        private readonly WaitTickProcessor _waitProcessor;
        private readonly Action<double, double> _tick;
        private bool _subscribed;

        public event Action<Timer> TimerStarted;
        public event Action<Timer> TimerStopped;
        public event Action<Timer> TimerPaused;
        public event Action<Timer> TimerResumed;
        public event Action<Timer> TimerCompleted;
        public event Action<Timer> TimerDestroyed;

        public TimeSystem() : this(TimeTicker.Instance) { TimeSystemPlayerLoop.Install(); }
        internal TimeSystem(TimeTicker ticker)
        {
            _ticker = ticker;
            _controller = new TimerController(_registry, Changed);
            _timers = new TimerTickProcessor(_registry, _controller);
            _waitProcessor = new WaitTickProcessor(_waits);
            _tick = Tick;
        }

        public Timer CreateTimer(TimerCreateOptions options) => _controller.Create(options);
        public Timer Get(string timerId) => _registry.Get(timerId);
        public bool TryGet(string timerId, out Timer timer) => _registry.TryGet(timerId, out timer);
        public void Start(string timerId) => _controller.Start(timerId, _ticker.CurrentTime, _ticker.CurrentUnscaledTime);
        public void Stop(string timerId) => _controller.Stop(timerId);
        public void Pause(string timerId) => _controller.Pause(timerId, _ticker.CurrentTime, _ticker.CurrentUnscaledTime);
        public void Resume(string timerId) => _controller.Resume(timerId, _ticker.CurrentTime, _ticker.CurrentUnscaledTime);
        public void DestroyTimer(string timerId) => _controller.Destroy(timerId, _ticker.CurrentTime, _ticker.CurrentUnscaledTime);

        public Awaitable Wait(double duration, TimerScaleMode scaleMode = TimerScaleMode.Scaled)
        {
            TimerController.ValidateTiming(duration, scaleMode);
            var now = scaleMode == TimerScaleMode.Scaled ? _ticker.CurrentTime : _ticker.CurrentUnscaledTime;
            var wait = _waits.Create(now + duration, scaleMode);
            RefreshSubscription();
            return wait.Completion.Awaitable;
        }

        private void Changed(TimerState state, Timer timer, bool resumed)
        {
            RefreshSubscription();
            switch (state)
            {
                case TimerState.Running:
                    if (resumed) TimerResumed?.Invoke(timer); else TimerStarted?.Invoke(timer);
                    break;
                case TimerState.Stopped: TimerStopped?.Invoke(timer); break;
                case TimerState.Paused: TimerPaused?.Invoke(timer); break;
                case TimerState.Completed: TimerCompleted?.Invoke(timer); break;
                case TimerState.Destroyed: TimerDestroyed?.Invoke(timer); break;
            }
        }

        internal void Tick(double scaled, double unscaled)
        {
            // Waits created by Timer callbacks also belong to the next tick.
            var lastWaitId = _waits.LastId;
            Exception timerError = null;
            try
            {
                try { _timers.Tick(scaled, unscaled); }
                catch (Exception error) { timerError = error; }
                try { _waitProcessor.Tick(scaled, unscaled, lastWaitId); }
                catch (Exception error)
                {
                    if (timerError != null) throw new AggregateException(timerError, error);
                    throw;
                }
                if (timerError != null) throw timerError;
            }
            finally { RefreshSubscription(); }
        }

        private void RefreshSubscription()
        {
            var needed = _waits.HasPending || _registry.HasActive;
            if (needed == _subscribed) return;
            _subscribed = needed;
            if (needed) _ticker.TimeTick += _tick;
            else _ticker.TimeTick -= _tick;
        }
    }
}
