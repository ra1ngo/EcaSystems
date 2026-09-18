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
        private EcaSystem _system;
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
            _system = TimeEcaSetup.CreateSystem(_time);
            _adapter = new TimeEcaAdapter(_time, _system.Events, _emitter);
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
        private void Attach() { _connector.Attach(_system); _adapter.Connect(); }

        [Test]
        public void EventKeys_MapEveryKeyToUniqueStableId()
        {
            var ids = Enum.GetValues(typeof(EcaTimeEventKey)).Cast<EcaTimeEventKey>().Select(EcaTimeEventIds.Get).ToArray();
            Assert.That(ids, Is.EqualTo(new[] { "time.timer.started", "time.timer.stopped", "time.timer.paused",
                "time.timer.resumed", "time.timer.completed", "time.timer.destroyed" }));
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length));
            Assert.Throws<ArgumentOutOfRangeException>(() => EcaTimeEventIds.Get((EcaTimeEventKey)(-1)));
        }

        [Test]
        public void CommandKeys_MapEveryKeyToUniqueStableId()
        {
            var ids = Enum.GetValues(typeof(EcaTimeCommandKey)).Cast<EcaTimeCommandKey>().Select(EcaTimeCommandIds.Get).ToArray();
            Assert.That(ids, Is.EqualTo(new[] { "time.timer.create", "time.timer.start", "time.timer.stop",
                "time.timer.pause", "time.timer.resume", "time.timer.destroy", "time.wait" }));
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length));
            Assert.Throws<ArgumentOutOfRangeException>(() => EcaTimeCommandIds.Get((EcaTimeCommandKey)(-1)));
        }

        [Test]
        public void WaitArguments_BelongToStandaloneTimeAssembly()
        {
            Assert.That(typeof(TimeWaitArgs).Assembly, Is.SameAs(typeof(TimeSystem).Assembly));
            Assert.That(typeof(TimeWaitArgs).Namespace, Is.EqualTo("EcaSystems.Time"));
            Assert.That(new EcaWaitCommand(_time).ArgsType, Is.EqualTo(typeof(TimeWaitArgs)));
        }

        [Test]
        public void Descriptor_ExportsLocalRegistriesAndSevenConcreteCommands()
        {
            Assert.That(_system.Id, Is.EqualTo("time"));
            Assert.That(_system.Namespace.Id, Is.EqualTo("time"));
            var typed = new[] { (IEcaEvent<EcaTimeEventState>)_system.Events.Resolve(EcaTimeEventIds.Get(EcaTimeEventKey.ECA_EVENT_TIMER_STARTED_ID)), (IEcaEvent<EcaTimeEventState>)_system.Events.Resolve(EcaTimeEventIds.Get(EcaTimeEventKey.ECA_EVENT_TIMER_STOPPED_ID)),
                (IEcaEvent<EcaTimeEventState>)_system.Events.Resolve(EcaTimeEventIds.Get(EcaTimeEventKey.ECA_EVENT_TIMER_PAUSED_ID)), (IEcaEvent<EcaTimeEventState>)_system.Events.Resolve(EcaTimeEventIds.Get(EcaTimeEventKey.ECA_EVENT_TIMER_RESUMED_ID)),
                (IEcaEvent<EcaTimeEventState>)_system.Events.Resolve(EcaTimeEventIds.Get(EcaTimeEventKey.ECA_EVENT_TIMER_COMPLETED_ID)), (IEcaEvent<EcaTimeEventState>)_system.Events.Resolve(EcaTimeEventIds.Get(EcaTimeEventKey.ECA_EVENT_TIMER_DESTROYED_ID)) };
            Assert.That(_system.Events.Events, Is.EqualTo(typed));
            Assert.That(typed.Select(e => e.Id), Is.EqualTo(new[] {
                "time.timer.started", "time.timer.stopped", "time.timer.paused",
                "time.timer.resumed", "time.timer.completed", "time.timer.destroyed" }));
            Assert.That(_system.Commands.Commands.Select(c => c.Id), Is.EquivalentTo(new[] {
                EcaTimeCommandIds.Get(EcaTimeCommandKey.ECA_COMMAND_TIMER_CREATE_ID), EcaTimeCommandIds.Get(EcaTimeCommandKey.ECA_COMMAND_TIMER_START_ID), EcaTimeCommandIds.Get(EcaTimeCommandKey.ECA_COMMAND_TIMER_STOP_ID),
                EcaTimeCommandIds.Get(EcaTimeCommandKey.ECA_COMMAND_TIMER_PAUSE_ID), EcaTimeCommandIds.Get(EcaTimeCommandKey.ECA_COMMAND_TIMER_RESUME_ID), EcaTimeCommandIds.Get(EcaTimeCommandKey.ECA_COMMAND_TIMER_DESTROY_ID), EcaTimeCommandIds.Get(EcaTimeCommandKey.ECA_COMMAND_WAIT_ID) }));
            Assert.That(_system.Commands.Commands.All(c => c.ContextType == typeof(IEcaActionContext)), Is.True);
            Assert.That(_system.Commands.Commands.Select(c => c.GetType()), Is.EquivalentTo(new[] {
                typeof(EcaCreateTimerCommand), typeof(EcaStartTimerCommand), typeof(EcaStopTimerCommand),
                typeof(EcaPauseTimerCommand), typeof(EcaResumeTimerCommand), typeof(EcaDestroyTimerCommand), typeof(EcaWaitCommand) }));
            Attach();
            foreach (var declaration in typed)
            {
                Assert.That(declaration, Is.TypeOf<EcaTimeEvent>());
                Assert.That(_events.Resolve(declaration.Id), Is.SameAs(declaration));
                Assert.That(_events.CheckRegistered(declaration), Is.True);
            }
        }

        [Test]
        public void Commands_AndLifecycleEvents_AdaptAllOperations()
        {
            Attach();
            var commands = Bind();
            Assert.That(commands.Run(EcaTimeCommandIds.Get(EcaTimeCommandKey.ECA_COMMAND_TIMER_CREATE_ID), new TimerCreateOptions("timer", 10)).IsCompletedSuccessfully, Is.True);
            commands.Run(EcaTimeCommandIds.Get(EcaTimeCommandKey.ECA_COMMAND_TIMER_START_ID), "timer").GetAwaiter().GetResult();
            commands.Run(EcaTimeCommandIds.Get(EcaTimeCommandKey.ECA_COMMAND_TIMER_PAUSE_ID), "timer").GetAwaiter().GetResult();
            commands.Run(EcaTimeCommandIds.Get(EcaTimeCommandKey.ECA_COMMAND_TIMER_RESUME_ID), "timer").GetAwaiter().GetResult();
            commands.Run(EcaTimeCommandIds.Get(EcaTimeCommandKey.ECA_COMMAND_TIMER_STOP_ID), "timer").GetAwaiter().GetResult();
            commands.Run(EcaTimeCommandIds.Get(EcaTimeCommandKey.ECA_COMMAND_TIMER_DESTROY_ID), "timer").GetAwaiter().GetResult();
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
            EcaTimeEventState snapshot = null;
            _emitter.OnFire = (id, state) =>
            {
                if (id != EcaTimeEventIds.Get(EcaTimeEventKey.ECA_EVENT_TIMER_COMPLETED_ID)) return;
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
            _connector.Detach(_system);
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
            var task = Bind().Run(EcaTimeCommandIds.Get(EcaTimeCommandKey.ECA_COMMAND_WAIT_ID), new TimeWaitArgs(0, mode));
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
            Assert.Throws<ArgumentOutOfRangeException>(() => commands.Run(EcaTimeCommandIds.Get(EcaTimeCommandKey.ECA_COMMAND_TIMER_CREATE_ID), new TimerCreateOptions("timer", -1)));
            Assert.Throws<ArgumentException>(() => commands.Run(EcaTimeCommandIds.Get(EcaTimeCommandKey.ECA_COMMAND_TIMER_START_ID), 12));
            Assert.Throws<InvalidOperationException>(() => commands.Run(EcaTimeCommandIds.Get(EcaTimeCommandKey.ECA_COMMAND_TIMER_START_ID), "missing"));
        }

        [Test]
        public void Adapter_ResolvesOnceAndForwardsCanonicalCachedInstances()
        {
            var local = new CountingRegistry(_system.Events);
            _adapter = new TimeEcaAdapter(_time, local, _emitter);
            Assert.That(local.ResolveCount, Is.EqualTo(6));
            Attach();
            _time.CreateTimer(new TimerCreateOptions("timer", 0));
            _time.Start("timer");
            _time.Pause("timer");
            _time.Resume("timer");
            Tick();
            _time.Stop("timer");
            _time.DestroyTimer("timer");
            Assert.That(local.ResolveCount, Is.EqualTo(6));
            Assert.That(_emitter.Declarations.Count, Is.EqualTo(6));
            foreach (var declaration in _emitter.Declarations)
                Assert.That(declaration, Is.SameAs(_system.Events.Resolve(declaration.Id)));
        }

        [Test]
        public void Adapter_RejectsAnyMissingDeclarationDuringConstruction()
        {
            foreach (var declaration in _system.Events.Events.ToArray())
            {
                _system.Events.Unregister(declaration.Id);
                Assert.Throws<InvalidOperationException>(() => new TimeEcaAdapter(_time, _system.Events, _emitter));
                _system.Events.Register(declaration);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Adapter_RejectsWrongInterfaceOrMetadata(bool wrongMetadata)
        {
            var id = EcaTimeEventIds.Get(EcaTimeEventKey.ECA_EVENT_TIMER_STARTED_ID);
            _system.Events.Unregister(id);
            _system.Events.Register(wrongMetadata
                ? (IEcaEvent)new Declaration<EcaTimeEventState>(id, typeof(int))
                : new Declaration<int>(id, typeof(EcaTimeEventState)));
            Assert.Throws<ArgumentException>(() => new TimeEcaAdapter(_time, _system.Events, _emitter));
        }

        [Test]
        public void Construction_RejectsNullDependencies()
        {
            Assert.Throws<ArgumentNullException>(() => new TimeEcaAdapter(null, _system.Events, _emitter));
            Assert.Throws<ArgumentNullException>(() => new TimeEcaAdapter(_time, null, _emitter));
            Assert.Throws<ArgumentNullException>(() => new TimeEcaAdapter(_time, _system.Events, null));
            Assert.Throws<ArgumentNullException>(() => TimeEcaSetup.CreateSystem(null));
            Assert.Throws<ArgumentNullException>(() => new EcaCreateTimerCommand(null));
            Assert.Throws<ArgumentNullException>(() => new EcaStartTimerCommand(null));
            Assert.Throws<ArgumentNullException>(() => new EcaStopTimerCommand(null));
            Assert.Throws<ArgumentNullException>(() => new EcaPauseTimerCommand(null));
            Assert.Throws<ArgumentNullException>(() => new EcaResumeTimerCommand(null));
            Assert.Throws<ArgumentNullException>(() => new EcaDestroyTimerCommand(null));
            Assert.Throws<ArgumentNullException>(() => new EcaWaitCommand(null));
        }

        private sealed class Declaration<E> : IEcaEvent<E>
        {
            public string Id { get; }
            public string Name => Id;
            public string Description => Id;
            public Type EventStateType { get; }
            internal Declaration(string id, Type type) { Id = id; EventStateType = type; }
        }

        private sealed class CountingRegistry : IEcaEventRegistry
        {
            private readonly IEcaEventRegistry _inner;
            internal int ResolveCount;
            internal CountingRegistry(IEcaEventRegistry inner) => _inner = inner;
            public IReadOnlyCollection<IEcaEvent> Events => _inner.Events;
            public void Register(IEcaEvent item) => _inner.Register(item);
            public bool Unregister(string id) => _inner.Unregister(id);
            public bool Contains(string id) => _inner.Contains(id);
            public bool CheckRegistered(IEcaEvent item) => _inner.CheckRegistered(item);
            public IEcaEvent Resolve(string id) { ResolveCount++; return _inner.Resolve(id); }
        }

        private static void Tick()
        {
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            Find(loop).updateDelegate();
        }

        private static PlayerLoopSystem Find(PlayerLoopSystem loop)
        {
            if (loop.type?.FullName == "EcaSystems.Time.TimeSystemPlayerLoop") return loop;
            if (loop.subSystemList != null)
                foreach (var child in loop.subSystemList)
                {
                    var found = Find(child);
                    if (found.type?.FullName == "EcaSystems.Time.TimeSystemPlayerLoop") return found;
                }
            return default;
        }

        private sealed class Context : IEcaActionContext { }

        // Public-contract test double: real registry validation, without changing
        // Core2 internal emitter binding or adding production composition.
        private sealed class RecordingEmitter : IEcaEventEmitter
        {
            private readonly EcaBaseEventRegistry _events;
            internal readonly List<(string id, EcaTimeEventState state)> Records = new();
            internal readonly List<IEcaEvent> Declarations = new();
            internal Action<string, EcaTimeEventState> OnFire;
            internal RecordingEmitter(EcaBaseEventRegistry events) => _events = events;
            public void Fire<E>(IEcaEvent<E> ecaEvent, E eventState)
            {
                if (!_events.CheckRegistered(ecaEvent)) throw new InvalidOperationException("Event is not registered.");
                Assert.That(ecaEvent.EventStateType, Is.EqualTo(typeof(E)));
                Assert.That(eventState, Is.TypeOf<EcaTimeEventState>());
                var state = (EcaTimeEventState)(object)eventState;
                Records.Add((ecaEvent.Id, state));
                Declarations.Add(ecaEvent);
                OnFire?.Invoke(ecaEvent.Id, state);
            }
        }
    }
}
