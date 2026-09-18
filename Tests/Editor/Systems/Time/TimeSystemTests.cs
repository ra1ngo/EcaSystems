using System;
using System.Collections.Generic;
using EcaSystems.Time;
using NUnit.Framework;

namespace EcaSystems.Tests.Time
{
    [TestFixture]
    public sealed class TimeSystemTests
    {
        private TimeSystem _system;
        private double _scaled;
        private double _unscaled;
        private TimeTicker _ticker;
        private readonly Dictionary<Timer, TimerEvents> _events = new();

        [SetUp]
        public void SetUp()
        {
            _scaled = 100;
            _unscaled = 200;
            _events.Clear();
            _ticker = new TimeTicker();
            _ticker.UpdateTime(_scaled, _unscaled);
            _system = new TimeSystem(_ticker);
        }

        // Adapt existing per-timer assertions to the new aggregate observation API.
        private TimerEvents Events(Timer timer)
        {
            if (_events.TryGetValue(timer, out var events)) return events;
            events = new TimerEvents(_system, timer);
            _events.Add(timer, events);
            return events;
        }

        private sealed class TimerEvents
        {
            internal event Action<Timer> Started, Stopped, Paused, Resumed, Completed, Destroyed;
            internal TimerEvents(TimeSystem system, Timer timer)
            {
                system.TimerStarted += t => { if (t == timer) Started?.Invoke(t); };
                system.TimerStopped += t => { if (t == timer) Stopped?.Invoke(t); };
                system.TimerPaused += t => { if (t == timer) Paused?.Invoke(t); };
                system.TimerResumed += t => { if (t == timer) Resumed?.Invoke(t); };
                system.TimerCompleted += t => { if (t == timer) Completed?.Invoke(t); };
                system.TimerDestroyed += t => { if (t == timer) Destroyed?.Invoke(t); };
            }
        }
        private Timer Create(double duration = 10, TimerScaleMode mode = TimerScaleMode.Scaled, string id = "timer") =>
            _system.CreateTimer(new TimerCreateOptions(id, duration, mode));

        private void Tick(double scaled, double unscaled)
        {
            _scaled += scaled;
            _unscaled += unscaled;
            _ticker.UpdateTime(_scaled, _unscaled); _system.Tick(_scaled, _unscaled);
        }

        private static void Values(Timer timer, TimerState state, double elapsed, double remaining, double progress)
        {
            Assert.That(timer.State, Is.EqualTo(state));
            Assert.That(timer.Elapsed, Is.EqualTo(elapsed).Within(1e-9));
            Assert.That(timer.Remaining, Is.EqualTo(remaining).Within(1e-9));
            Assert.That(timer.Progress, Is.EqualTo(progress).Within(1e-9));
        }

        [Test]
        public void Create_MetadataInitialValuesAndExactLookup()
        {
            var timer = Create(mode: TimerScaleMode.Unscaled);
            Assert.That(timer.Id, Is.EqualTo("timer"));
            Assert.That(timer.Duration, Is.EqualTo(10));
            Assert.That(timer.ScaleMode, Is.EqualTo(TimerScaleMode.Unscaled));
            Values(timer, TimerState.Stopped, 0, 10, 0);
            Assert.That(_system.Get("timer"), Is.SameAs(timer));
            Assert.That(_system.TryGet("timer", out var found), Is.True);
            Assert.That(found, Is.SameAs(timer));
            Assert.That(_system.TryGet("missing", out found), Is.False);
            Assert.That(found, Is.Null);
            Assert.That(_ticker.SubscriberCount, Is.Zero);
        }

        [Test]
        public void Registry_DuplicateOrdinalAndInstanceLocalIds()
        {
            var original = Create();
            Assert.Throws<InvalidOperationException>(() => Create());
            Assert.That(_system.Get("timer"), Is.SameAs(original));
            Assert.That(Create(id: "Timer"), Is.Not.SameAs(original));
            var other = new TimeSystem(new TimeTicker());
            Assert.That(other.CreateTimer(new TimerCreateOptions("timer", 1)), Is.Not.SameAs(original));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" \t")]
        public void InvalidId_AllFacadeOperationsReject(string id)
        {
            Assert.Throws<ArgumentException>(() => Create(id: id));
            Assert.Throws<ArgumentException>(() => _system.Get(id));
            Assert.Throws<ArgumentException>(() => _system.TryGet(id, out _));
            Assert.Throws<ArgumentException>(() => _system.Start(id));
            Assert.Throws<ArgumentException>(() => _system.Stop(id));
            Assert.Throws<ArgumentException>(() => _system.Pause(id));
            Assert.Throws<ArgumentException>(() => _system.Resume(id));
            Assert.Throws<ArgumentException>(() => _system.DestroyTimer(id));
        }

        [TestCase(-1)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void InvalidDuration_CreateAndWaitReject(double duration)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(duration));
            Assert.Throws<ArgumentOutOfRangeException>(() => _system.Wait(duration));
            Assert.That(_system.TryGet("timer", out _), Is.False);
            Assert.That(_ticker.SubscriberCount, Is.Zero);
        }

        [Test]
        public void NullOptionsAndInvalidScaleReject()
        {
            Assert.Throws<ArgumentNullException>(() => _system.CreateTimer(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(mode: (TimerScaleMode)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => _system.Wait(1, (TimerScaleMode)99));
        }

        [Test]
        public void UnknownAndDestroyedId_AllMutationsReject()
        {
            Create();
            _system.DestroyTimer("timer");
            foreach (var id in new[] { "timer", "missing" })
            {
                Assert.Throws<InvalidOperationException>(() => _system.Get(id));
                Assert.Throws<InvalidOperationException>(() => _system.Start(id));
                Assert.Throws<InvalidOperationException>(() => _system.Stop(id));
                Assert.Throws<InvalidOperationException>(() => _system.Pause(id));
                Assert.Throws<InvalidOperationException>(() => _system.Resume(id));
                Assert.Throws<InvalidOperationException>(() => _system.DestroyTimer(id));
                Assert.That(_system.TryGet(id, out _), Is.False);
            }
        }

        [Test]
        public void Lifecycle_StartPauseResumeCompletionRestartAndStop()
        {
            var timer = Create();
            var events = new List<string>();
            Events(timer).Started += value => { Assert.That(value, Is.SameAs(timer)); events.Add("start"); };
            Events(timer).Paused += _ => events.Add("pause");
            Events(timer).Resumed += _ => events.Add("resume");
            Events(timer).Completed += _ => events.Add("complete");
            Events(timer).Stopped += _ => events.Add("stop");
            _system.Start("timer");
            Values(timer, TimerState.Running, 0, 10, 0);
            Tick(3, 3);
            Values(timer, TimerState.Running, 3, 7, .3);
            _system.Pause("timer");
            Assert.That(_ticker.SubscriberCount, Is.Zero);
            Tick(50, 50);
            Values(timer, TimerState.Paused, 3, 7, .3);
            _system.Resume("timer");
            Assert.That(_ticker.SubscriberCount, Is.EqualTo(1));
            Tick(6, 6);
            Values(timer, TimerState.Running, 9, 1, .9);
            Tick(1, 1);
            Values(timer, TimerState.Completed, 10, 0, 1);
            Assert.That(_system.Get("timer"), Is.SameAs(timer));
            Tick(50, 50);
            Assert.That(_ticker.SubscriberCount, Is.Zero);
            _system.Start("timer");
            Values(timer, TimerState.Running, 0, 10, 0);
            _system.Stop("timer");
            Values(timer, TimerState.Stopped, 0, 10, 0);
            Assert.That(events, Is.EqualTo(new[] { "start", "pause", "resume", "complete", "start", "stop" }));
        }

        [TestCase(TimerState.Running)]
        [TestCase(TimerState.Paused)]
        [TestCase(TimerState.Completed)]
        public void Stop_ResetsEachAllowedState(TimerState state)
        {
            var timer = Create();
            MoveTo(state);
            var count = 0;
            Events(timer).Stopped += t => { Values(t, TimerState.Stopped, 0, 10, 0); count++; };
            _system.Stop("timer");
            Tick(100, 100);
            Values(timer, TimerState.Stopped, 0, 10, 0);
            Assert.That(count, Is.EqualTo(1));
            Assert.That(_ticker.SubscriberCount, Is.Zero);
        }

        [TestCase(TimerState.Stopped)]
        [TestCase(TimerState.Running)]
        [TestCase(TimerState.Paused)]
        [TestCase(TimerState.Completed)]
        public void Destroy_UnregistersBeforeEvent_PreservesValuesAndAllowsReplacement(TimerState state)
        {
            var timer = Create();
            MoveTo(state);
            var elapsed = timer.Elapsed;
            var progress = timer.Progress;
            var events = new List<string>();
            Events(timer).Stopped += _ => events.Add("stop");
            Events(timer).Completed += _ => events.Add("complete");
            Timer replacement = null;
            Events(timer).Destroyed += t =>
            {
                Assert.That(t, Is.SameAs(timer));
                Assert.That(t.State, Is.EqualTo(TimerState.Destroyed));
                Assert.That(_system.TryGet(t.Id, out _), Is.False);
                replacement = Create();
                events.Add("destroy");
            };
            _system.DestroyTimer("timer");
            _system.Start("timer");
            Tick(1, 1);
            Values(timer, TimerState.Destroyed, elapsed, 10 - elapsed, progress);
            Assert.That(_system.Get("timer"), Is.SameAs(replacement));
            Assert.That(events, Is.EqualTo(new[] { "destroy" }));
        }

        [TestCase(TimerState.Running, "start")]
        [TestCase(TimerState.Paused, "start")]
        [TestCase(TimerState.Stopped, "pause")]
        [TestCase(TimerState.Paused, "pause")]
        [TestCase(TimerState.Completed, "pause")]
        [TestCase(TimerState.Stopped, "resume")]
        [TestCase(TimerState.Running, "resume")]
        [TestCase(TimerState.Completed, "resume")]
        [TestCase(TimerState.Stopped, "stop")]
        public void InvalidTransition_LeavesStateAndEventsUnchanged(TimerState state, string operation)
        {
            var timer = Create();
            MoveTo(state);
            var count = 0;
            Events(timer).Started += _ => count++;
            Events(timer).Paused += _ => count++;
            Events(timer).Resumed += _ => count++;
            Events(timer).Stopped += _ => count++;
            Action action = operation switch
            {
                "start" => () => _system.Start("timer"),
                "pause" => () => _system.Pause("timer"),
                "resume" => () => _system.Resume("timer"),
                _ => () => _system.Stop("timer")
            };
            Assert.Throws<InvalidOperationException>(() => action());
            Assert.That(timer.State, Is.EqualTo(state));
            Assert.That(count, Is.Zero);
        }

        private void MoveTo(TimerState state)
        {
            if (state == TimerState.Stopped) return;
            _system.Start("timer");
            Tick(state == TimerState.Completed ? 10 : 2, 2);
            if (state == TimerState.Paused) _system.Pause("timer");
        }

        [Test]
        public void ScaleMode_UsesIndependentClockAtRunnerBoundary()
        {
            var scaled = Create();
            var unscaled = Create(mode: TimerScaleMode.Unscaled, id: "unscaled");
            _system.Start(scaled.Id);
            _system.Start(unscaled.Id);
            Tick(0, 5);
            Values(scaled, TimerState.Running, 0, 10, 0);
            Values(unscaled, TimerState.Running, 5, 5, .5);
            Tick(2, 5);
            Values(scaled, TimerState.Running, 2, 8, .2);
            Values(unscaled, TimerState.Completed, 10, 0, 1);
        }

        [Test]
        public void ReadValues_AreFrameData_UseAbsoluteDoubleTimeOnTick_ClampOvershoot()
        {
            var timer = Create(.125);
            _system.Start(timer.Id);
            _scaled += .03125;
            Values(timer, TimerState.Running, 0, .125, 0);
            _system.Tick(_scaled, _unscaled);
            Values(timer, TimerState.Running, .03125, .09375, .25);
            _scaled += 100;
            Values(timer, TimerState.Running, .03125, .09375, .25);
            _ticker.UpdateTime(_scaled, _unscaled); _system.Tick(_scaled, _unscaled);
            Values(timer, TimerState.Completed, .125, 0, 1);
        }

        [Test]
        public void ZeroDuration_StartedBeforeNextTickCompletion_ProgressSurvivesDestroy()
        {
            var timer = Create(0);
            var events = new List<string>();
            Events(timer).Started += _ => events.Add("start");
            Events(timer).Completed += _ => events.Add("complete");
            Values(timer, TimerState.Stopped, 0, 0, 0);
            _system.Start(timer.Id);
            Values(timer, TimerState.Running, 0, 0, 0);
            Assert.That(events, Is.EqualTo(new[] { "start" }));
            Tick(0, 0);
            Values(timer, TimerState.Completed, 0, 0, 1);
            _system.DestroyTimer(timer.Id);
            Values(timer, TimerState.Destroyed, 0, 0, 1);
            Assert.That(events, Is.EqualTo(new[] { "start", "complete" }));
        }

        [Test]
        public void CompletionCallback_CanRestartWithoutLosingNewRun()
        {
            var timer = Create(0);
            var count = 0;
            Events(timer).Completed += t => { count++; if (count == 1) _system.Start(t.Id); };
            _system.Start(timer.Id);
            Tick(0, 0);
            Assert.That(count, Is.EqualTo(1));
            Assert.That(timer.State, Is.EqualTo(TimerState.Running));
            Assert.That(_ticker.SubscriberCount, Is.EqualTo(1));
            Tick(0, 0);
            Assert.That(count, Is.EqualTo(2));
            Assert.That(_ticker.SubscriberCount, Is.Zero);
        }

        [Test]
        public void Callback_RestartsLaterTimer_NewRunWaitsForNextTick()
        {
            var first = Create(0);
            var later = Create(0, id: "later");
            Events(first).Completed += _ => { _system.Stop(later.Id); _system.Start(later.Id); };
            _system.Start(first.Id);
            _system.Start(later.Id);
            Tick(0, 0);
            Assert.That(later.State, Is.EqualTo(TimerState.Running));
            Tick(0, 0);
            Assert.That(later.State, Is.EqualTo(TimerState.Completed));
        }

        [Test]
        public void StartedCallback_CanDestroyAndReplaceWithoutRunningOldTimer()
        {
            var old = Create(0);
            Events(old).Started += t => { _system.DestroyTimer(t.Id); Create(0); _system.Start(t.Id); };
            _system.Start(old.Id);
            Tick(0, 0);
            Assert.That(old.State, Is.EqualTo(TimerState.Destroyed));
            Assert.That(_system.Get(old.Id).State, Is.EqualTo(TimerState.Completed));
        }

        [Test]
        public void NestedTick_DoesNotOverwriteOuterSnapshot()
        {
            var first = Create(0);
            var later = Create(0, id: "later");
            var completed = new List<string>();
            Events(first).Completed += _ =>
            {
                completed.Add("first");
                _ticker.UpdateTime(_scaled, _unscaled); _system.Tick(_scaled, _unscaled);
                _system.Start(later.Id);
            };
            Events(later).Completed += _ => completed.Add("later");
            _system.Start(first.Id);
            _system.Start(later.Id);
            Tick(0, 0);
            Assert.That(completed, Is.EqualTo(new[] { "first", "later" }));
            Assert.That(later.State, Is.EqualTo(TimerState.Running));
            Tick(0, 0);
            Assert.That(completed, Is.EqualTo(new[] { "first", "later", "later" }));
        }

        [Test]
        public void WarmRegistryTick_DoesNotAllocateAtSameOrSmallerCount()
        {
            for (var i = 0; i < 16; i++)
            {
                var timer = Create(id: "timer-" + i);
                _system.Start(timer.Id);
            }
            for (var i = 0; i < 10; i++) _system.Tick(_scaled, _unscaled);
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 100; i++) _system.Tick(_scaled, _unscaled);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
            for (var i = 1; i < 16; i++) _system.DestroyTimer("timer-" + i);
            before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 100; i++) _system.Tick(_scaled, _unscaled);
            allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
        }

        [Test]
        public void CallbackErrors_KeepCommittedTransitionsAndDoNotStarveOtherTimers()
        {
            var first = Create(0);
            var second = Create(0, id: "second");
            Events(first).Started += _ => throw new InvalidOperationException("start callback");
            Events(first).Completed += _ => throw new InvalidOperationException("completion callback");
            Assert.Throws<InvalidOperationException>(() => _system.Start(first.Id));
            _system.Start(second.Id);
            var error = Assert.Throws<AggregateException>(() => Tick(0, 0));
            Assert.That(error.InnerExceptions.Count, Is.EqualTo(1));
            Assert.That(first.State, Is.EqualTo(TimerState.Completed));
            Assert.That(second.State, Is.EqualTo(TimerState.Completed));
            Assert.That(_ticker.SubscriberCount, Is.Zero);
        }
    }
}
