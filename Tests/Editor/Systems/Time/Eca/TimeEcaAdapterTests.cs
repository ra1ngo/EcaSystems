using System;
using System.Collections.Generic;
using System.Linq;
using EcaSystems.Core2;
using EcaSystems.Time;
using EcaSystems.Time.Eca;
using NUnit.Framework;
using UnityEngine.LowLevel;

namespace EcaSystems.Tests.TimeEca
{
    public sealed class TimeEcaAdapterTests
    {
        private TimeSystem _time;
        private TimeEcaAdapter _adapter;
        private EcaBaseEventRegistry _events;
        private EcaCommandRegistry _commands;
        private EcaSystemConnector _connector;
        private RecordingEmitter _emitter;
        private PlayerLoopSystem _original;

        [SetUp]
        public void SetUp()
        {
            _original = PlayerLoop.GetCurrentPlayerLoop();
            _time = new TimeSystem();
            _events = new EcaBaseEventRegistry();
            _commands = new EcaCommandRegistry();
            _connector = new EcaSystemConnector(new EcaSystemRegistry(), new EcaSystemNamespaceRegistry(), _events, _commands);
            _emitter = new RecordingEmitter(_events);
            _adapter = new TimeEcaAdapter(_time, _emitter);
        }

        [TearDown]
        public void TearDown()
        {
            _adapter.Disconnect();
            if (_time.TryGet("timer", out _)) _time.DestroyTimer("timer");
            Tick();
            PlayerLoop.SetPlayerLoop(_original);
        }

        private IEcaCommands Bind() => new EcaCommandRunner(_commands).Bind(new Context());
        private void Attach() { _connector.Attach(_adapter.System); _adapter.Connect(); }

        [Test]
        public void Descriptor_ExportsExactTypedCatalogAndSevenCommands()
        {
            Assert.That(_adapter.System.Id, Is.EqualTo("time"));
            Assert.That(_adapter.System.Namespace.Id, Is.EqualTo("time"));
            var typed = new[] { _adapter.Events.TimerStarted, _adapter.Events.TimerStopped,
                _adapter.Events.TimerPaused, _adapter.Events.TimerResumed,
                _adapter.Events.TimerCompleted, _adapter.Events.TimerDestroyed };
            Assert.That(_adapter.System.Events, Is.EqualTo(typed));
            Assert.That(typed.Select(e => e.Id), Is.EqualTo(new[] {
                "time.timer.started", "time.timer.stopped", "time.timer.paused",
                "time.timer.resumed", "time.timer.completed", "time.timer.destroyed" }));
            Assert.That(_adapter.System.Commands.Select(c => c.Id), Is.EquivalentTo(new[] {
                TimeEcaCommandIds.CreateTimer, TimeEcaCommandIds.StartTimer, TimeEcaCommandIds.StopTimer,
                TimeEcaCommandIds.PauseTimer, TimeEcaCommandIds.ResumeTimer, TimeEcaCommandIds.DestroyTimer, TimeEcaCommandIds.Wait }));
            Assert.That(_adapter.System.Commands.All(c => c.ContextType == typeof(IEcaActionContext)), Is.True);
        }

        [Test]
        public void Commands_AndLifecycleEvents_AdaptAllOperations()
        {
            Attach();
            var commands = Bind();
            Assert.That(commands.Run(TimeEcaCommandIds.CreateTimer, new TimerCreateOptions("timer", 10)).IsCompletedSuccessfully, Is.True);
            commands.Run(TimeEcaCommandIds.StartTimer, "timer").GetAwaiter().GetResult();
            commands.Run(TimeEcaCommandIds.PauseTimer, "timer").GetAwaiter().GetResult();
            commands.Run(TimeEcaCommandIds.ResumeTimer, "timer").GetAwaiter().GetResult();
            commands.Run(TimeEcaCommandIds.StopTimer, "timer").GetAwaiter().GetResult();
            commands.Run(TimeEcaCommandIds.DestroyTimer, "timer").GetAwaiter().GetResult();
            Assert.That(_time.TryGet("timer", out _), Is.False);
            Assert.That(_emitter.Records.Select(r => r.state.State), Is.EqualTo(new[] {
                TimerState.Running, TimerState.Paused, TimerState.Running, TimerState.Stopped, TimerState.Destroyed }));
            Assert.That(_emitter.Records.Select(r => r.id), Is.EqualTo(new[] {
                "time.timer.started", "time.timer.paused", "time.timer.resumed", "time.timer.stopped", "time.timer.destroyed" }));
            var started = _emitter.Records[0].state;
            Assert.That(started.TimerId, Is.EqualTo("timer"));
            Assert.That(started.Duration, Is.EqualTo(10));
            Assert.That(started.Elapsed, Is.Zero);
            Assert.That(started.Remaining, Is.EqualTo(10));
            Assert.That(started.Progress, Is.Zero);
            Assert.That(started.ScaleMode, Is.EqualTo(TimerScaleMode.Scaled));
        }

        [Test]
        public void CompletionSnapshot_RemainsCompletedAfterReentrantRestart()
        {
            Attach();
            var timer = _time.CreateTimer(new TimerCreateOptions("timer", 0));
            TimerEventState snapshot = null;
            _emitter.OnFire = (id, state) =>
            {
                if (id != "time.timer.completed") return;
                snapshot = state;
                _time.Start("timer");
                Assert.That(state.State, Is.EqualTo(TimerState.Completed));
            };
            _time.Start("timer");
            Tick();
            Assert.That(timer.State, Is.EqualTo(TimerState.Running));
            Assert.That(snapshot.State, Is.EqualTo(TimerState.Completed));
            Assert.That(snapshot.Progress, Is.EqualTo(1));
            Assert.That(timer.Progress, Is.Zero);
        }

        [Test]
        public void Connection_IsExplicit_DisconnectBeforeDetachStopsAllForwarding()
        {
            Attach();
            Assert.Throws<InvalidOperationException>(() => _adapter.Connect());
            _adapter.Disconnect();
            _adapter.Disconnect();
            _connector.Detach(_adapter.System);
            _time.CreateTimer(new TimerCreateOptions("timer", 0));
            _time.Start("timer");
            _time.Pause("timer");
            _time.Resume("timer");
            Tick();
            _time.Stop("timer");
            _time.DestroyTimer("timer");
            Assert.That(_emitter.Records, Is.Empty);
            Attach();
            _time.CreateTimer(new TimerCreateOptions("timer", 0));
            _time.Start("timer");
            Assert.That(_emitter.Records.Count, Is.EqualTo(1));
        }

        [Test]
        public void ConnectBeforeAttach_ExposesRegistrationContract()
        {
            _adapter.Connect();
            _time.CreateTimer(new TimerCreateOptions("timer", 1));
            Assert.Throws<InvalidOperationException>(() => _time.Start("timer"));
            Assert.That(_time.Get("timer").IsActive, Is.True);
            Assert.That(_emitter.Records, Is.Empty);
        }

        [TestCase(TimerScaleMode.Scaled)]
        [TestCase(TimerScaleMode.Unscaled)]
        public void WaitCommand_TaskRemainsPendingUntilUnderlyingTick(TimerScaleMode mode)
        {
            Attach();
            var task = Bind().Run(TimeEcaCommandIds.Wait, new TimeWaitArgs(0, mode));
            Assert.That(task.IsCompleted, Is.False);
            Assert.That(_time.TryGet("timer", out _), Is.False);
            Tick();
            Assert.That(task.IsCompletedSuccessfully, Is.True);
            Assert.That(_emitter.Records, Is.Empty);
        }

        [Test]
        public void Commands_PreserveValidationAndTypedArguments()
        {
            Attach();
            var commands = Bind();
            Assert.Throws<ArgumentOutOfRangeException>(() => commands.Run(TimeEcaCommandIds.CreateTimer, new TimerCreateOptions("timer", -1)));
            Assert.Throws<ArgumentException>(() => commands.Run(TimeEcaCommandIds.StartTimer, 12));
            Assert.Throws<InvalidOperationException>(() => commands.Run(TimeEcaCommandIds.StartTimer, "missing"));
        }

        private static void Tick()
        {
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            Find(loop).updateDelegate();
        }

        private static PlayerLoopSystem Find(PlayerLoopSystem loop)
        {
            if (loop.type == typeof(TimeTicker)) return loop;
            if (loop.subSystemList != null)
                foreach (var child in loop.subSystemList)
                {
                    var found = Find(child);
                    if (found.type == typeof(TimeTicker)) return found;
                }
            return default;
        }

        private sealed class Context : IEcaActionContext { }

        // Public-contract test double: real registry validation, without changing
        // Core2 internal emitter binding or adding production composition.
        private sealed class RecordingEmitter : IEcaEventEmitter
        {
            private readonly EcaBaseEventRegistry _events;
            internal readonly List<(string id, TimerEventState state)> Records = new();
            internal Action<string, TimerEventState> OnFire;
            internal RecordingEmitter(EcaBaseEventRegistry events) => _events = events;
            public void Fire<E>(IEcaEvent<E> ecaEvent, E eventState)
            {
                if (!_events.CheckRegistered(ecaEvent)) throw new InvalidOperationException("Event is not registered.");
                Assert.That(ecaEvent.EventStateType, Is.EqualTo(typeof(E)));
                Assert.That(eventState, Is.TypeOf<TimerEventState>());
                var state = (TimerEventState)(object)eventState;
                Records.Add((ecaEvent.Id, state));
                OnFire?.Invoke(ecaEvent.Id, state);
            }
        }
    }
}
