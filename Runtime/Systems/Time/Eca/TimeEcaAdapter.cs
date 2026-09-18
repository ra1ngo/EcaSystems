using System;
using System.Threading.Tasks;
using EcaSystems.Core2;

namespace EcaSystems.Time.Eca
{
    /// <summary>Attach System exports before Connect; Disconnect before detaching exports.</summary>
    public sealed class TimeEcaAdapter
    {
        private readonly TimeSystem _time;
        private readonly IEcaEventEmitter _emitter;
        private bool _connected;
        public EcaSystem System { get; }
        public TimeEcaEvents Events { get; } = new();

        public TimeEcaAdapter(TimeSystem time, IEcaEventEmitter emitter)
        {
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
            System = new EcaSystem("time", new EcaSystemNamespace("time"),
                new IEcaEvent[] { Events.TimerStarted, Events.TimerStopped, Events.TimerPaused,
                    Events.TimerResumed, Events.TimerCompleted, Events.TimerDestroyed },
                new AEcaCommand[]
                {
                    new Command<TimerCreateOptions>(TimeEcaCommandIds.CreateTimer, options =>
                    { _time.CreateTimer(options); return Task.CompletedTask; }),
                    LifecycleCommand(TimeEcaCommandIds.StartTimer, _time.Start),
                    LifecycleCommand(TimeEcaCommandIds.StopTimer, _time.Stop),
                    LifecycleCommand(TimeEcaCommandIds.PauseTimer, _time.Pause),
                    LifecycleCommand(TimeEcaCommandIds.ResumeTimer, _time.Resume),
                    LifecycleCommand(TimeEcaCommandIds.DestroyTimer, _time.DestroyTimer),
                    new Command<TimeWaitArgs>(TimeEcaCommandIds.Wait, RunWait)
                }, name: "Time", description: "Standalone TimeSystem lifecycle and waits");
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

        private void Started(Timer timer) => _emitter.Fire(Events.TimerStarted, new TimerEventState(timer));
        private void Stopped(Timer timer) => _emitter.Fire(Events.TimerStopped, new TimerEventState(timer));
        private void Paused(Timer timer) => _emitter.Fire(Events.TimerPaused, new TimerEventState(timer));
        private void Resumed(Timer timer) => _emitter.Fire(Events.TimerResumed, new TimerEventState(timer));
        private void Completed(Timer timer) => _emitter.Fire(Events.TimerCompleted, new TimerEventState(timer));
        private void Destroyed(Timer timer) => _emitter.Fire(Events.TimerDestroyed, new TimerEventState(timer));
        private async Task RunWait(TimeWaitArgs args) => await _time.Wait(args.Duration, args.ScaleMode);

        private static AEcaCommand LifecycleCommand(string id, Action<string> action) =>
            new Command<string>(id, timerId => { action(timerId); return Task.CompletedTask; });

        private sealed class Command<A> : AEcaCommand<IEcaActionContext, A>
        {
            public override string Id { get; }
            private readonly Func<A, Task> _run;
            internal Command(string id, Func<A, Task> run) { Id = id; _run = run; }
            public override Task Run(IEcaActionContext context, A args) => _run(args);
        }
    }
}
