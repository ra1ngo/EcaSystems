using System;
using EcaSystems.Core2;

namespace EcaSystems.Time.Eca
{
    /// <summary>Attach exports before Connect; Disconnect before detaching exports.</summary>
    public sealed class TimeEcaAdapter
    {
        public const string ECA_EVENT_TIMER_STARTED_ID = "time.timer.started";
        public const string ECA_EVENT_TIMER_STOPPED_ID = "time.timer.stopped";
        public const string ECA_EVENT_TIMER_PAUSED_ID = "time.timer.paused";
        public const string ECA_EVENT_TIMER_RESUMED_ID = "time.timer.resumed";
        public const string ECA_EVENT_TIMER_COMPLETED_ID = "time.timer.completed";
        public const string ECA_EVENT_TIMER_DESTROYED_ID = "time.timer.destroyed";

        private readonly TimeSystem _time;
        private readonly IEcaEventEmitter _emitter;
        private readonly IEcaEvent<EcaTimeEventState> _started, _stopped, _paused, _resumed, _completed, _destroyed;
        private bool _connected;

        public TimeEcaAdapter(TimeSystem time, IEcaEventRegistry events, IEcaEventEmitter emitter)
        {
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
            if (events == null) throw new ArgumentNullException(nameof(events));
            _started = Resolve(events, ECA_EVENT_TIMER_STARTED_ID);
            _stopped = Resolve(events, ECA_EVENT_TIMER_STOPPED_ID);
            _paused = Resolve(events, ECA_EVENT_TIMER_PAUSED_ID);
            _resumed = Resolve(events, ECA_EVENT_TIMER_RESUMED_ID);
            _completed = Resolve(events, ECA_EVENT_TIMER_COMPLETED_ID);
            _destroyed = Resolve(events, ECA_EVENT_TIMER_DESTROYED_ID);
        }

        private static IEcaEvent<EcaTimeEventState> Resolve(IEcaEventRegistry events, string id)
        {
            var declaration = events.Resolve(id);
            if (declaration is not IEcaEvent<EcaTimeEventState> typed || declaration.EventStateType != typeof(EcaTimeEventState))
                throw new ArgumentException($"Event '{id}' must declare IEcaEvent<EcaTimeEventState> and matching metadata.", nameof(events));
            return typed;
        }
        public void Connect()
        {
            if (_connected) throw new InvalidOperationException("Time adapter is already connected.");
            _time.TimerStarted += Started;
            _time.TimerStopped += Stopped;
            _time.TimerPaused += Paused;
            _time.TimerResumed += Resumed;
            _time.TimerCompleted += Completed;
            _time.TimerDestroyed += Destroyed;
            _connected = true;
        }

        public void Disconnect()
        {
            if (!_connected) return;
            _time.TimerStarted -= Started;
            _time.TimerStopped -= Stopped;
            _time.TimerPaused -= Paused;
            _time.TimerResumed -= Resumed;
            _time.TimerCompleted -= Completed;
            _time.TimerDestroyed -= Destroyed;
            _connected = false;
        }

        private void Started(Timer timer) => _emitter.Fire(_started, new EcaTimeEventState(timer));
        private void Stopped(Timer timer) => _emitter.Fire(_stopped, new EcaTimeEventState(timer));
        private void Paused(Timer timer) => _emitter.Fire(_paused, new EcaTimeEventState(timer));
        private void Resumed(Timer timer) => _emitter.Fire(_resumed, new EcaTimeEventState(timer));
        private void Completed(Timer timer) => _emitter.Fire(_completed, new EcaTimeEventState(timer));
        private void Destroyed(Timer timer) => _emitter.Fire(_destroyed, new EcaTimeEventState(timer));
    }
}
