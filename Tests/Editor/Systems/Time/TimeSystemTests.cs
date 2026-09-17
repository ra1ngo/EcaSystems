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
        private readonly HashSet<TimerRunner> _active = new();

        [SetUp]
        public void SetUp()
        {
            _scaled = 100;
            _unscaled = 200;
            _active.Clear();
            _system = new TimeSystem(mode => mode == TimerScaleMode.Scaled ? _scaled : _unscaled,
                (runner, active) => { if (active) _active.Add(runner); else _active.Remove(runner); });
        }

        private Timer Create(double duration = 10, TimerScaleMode mode = TimerScaleMode.Scaled, string id = "timer") =>
            _system.CreateTimer(new TimerCreateOptions(id, duration, mode));

        private void Tick(double scaled, double unscaled)
        {
            _scaled += scaled;
            _unscaled += unscaled;
            _system.Tick(_scaled, _unscaled);
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
            Assert.That(_active, Is.Empty);
        }

        [Test]
        public void Registry_DuplicateOrdinalAndInstanceLocalIds()
        {
            var original = Create();
            Assert.Throws<InvalidOperationException>(() => Create());
            Assert.That(_system.Get("timer"), Is.SameAs(original));
            Assert.That(Create(id: "Timer"), Is.Not.SameAs(original));
            var other = new TimeSystem(_ => 0, (_, __) => { });
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
            Assert.That(_active, Is.Empty);
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
            timer.Started += value => { Assert.That(value, Is.SameAs(timer)); events.Add("start"); };
            timer.Paused += _ => events.Add("pause");
            timer.Resumed += _ => events.Add("resume");
            timer.Completed += _ => events.Add("complete");
            timer.Stopped += _ => events.Add("stop");
            _system.Start("timer");
            Values(timer, TimerState.Running, 0, 10, 0);
            Tick(3, 3);
            Values(timer, TimerState.Running, 3, 7, .3);
            _system.Pause("timer");
            Assert.That(_active, Is.Empty);
            Tick(50, 50);
            Values(timer, TimerState.Paused, 3, 7, .3);
            _system.Resume("timer");
            Assert.That(_active.Count, Is.EqualTo(1));
            Tick(6, 6);
            Values(timer, TimerState.Running, 9, 1, .9);
            Tick(1, 1);
            Values(timer, TimerState.Completed, 10, 0, 1);
            Assert.That(_system.Get("timer"), Is.SameAs(timer));
            Tick(50, 50);
            Assert.That(_active, Is.Empty);
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
            timer.Stopped += t => { Values(t, TimerState.Stopped, 0, 10, 0); count++; };
            _system.Stop("timer");
            Tick(100, 100);
            Values(timer, TimerState.Stopped, 0, 10, 0);
            Assert.That(count, Is.EqualTo(1));
            Assert.That(_active, Is.Empty);
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
            timer.Stopped += _ => events.Add("stop");
            timer.Completed += _ => events.Add("complete");
            Timer replacement = null;
            timer.Destroyed += t =>
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
            Assert.Throws<InvalidOperationException>(() => timer.Start());
            Assert.Throws<InvalidOperationException>(() => timer.Stop());
            Assert.Throws<InvalidOperationException>(() => timer.Pause());
            Assert.Throws<InvalidOperationException>(() => timer.Resume());
            Assert.Throws<InvalidOperationException>(() => timer.Destroy());
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
            timer.Started += _ => count++;
            timer.Paused += _ => count++;
            timer.Resumed += _ => count++;
            timer.Stopped += _ => count++;
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
        public void ReadValues_UseAbsoluteDoubleTimeWithoutNeedingTick_ClampOvershoot()
        {
            var timer = Create(.125);
            _system.Start(timer.Id);
            _scaled += .03125;
            Values(timer, TimerState.Running, .03125, .09375, .25);
            _scaled += 100;
            Values(timer, TimerState.Running, .125, 0, 1);
            _system.Tick(_scaled, _unscaled);
            Values(timer, TimerState.Completed, .125, 0, 1);
        }

        [Test]
        public void ZeroDuration_StartedBeforeNextTickCompletion_ProgressSurvivesDestroy()
        {
            var timer = Create(0);
            var events = new List<string>();
            timer.Started += _ => events.Add("start");
            timer.Completed += _ => events.Add("complete");
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
            timer.Completed += t => { count++; if (count == 1) _system.Start(t.Id); };
            _system.Start(timer.Id);
            Tick(0, 0);
            Assert.That(count, Is.EqualTo(1));
            Assert.That(timer.State, Is.EqualTo(TimerState.Running));
            Assert.That(_active.Count, Is.EqualTo(1));
            Tick(0, 0);
            Assert.That(count, Is.EqualTo(2));
            Assert.That(_active, Is.Empty);
        }

        [Test]
        public void Callback_RestartsLaterTimer_NewRunWaitsForNextTick()
        {
            var first = Create(0);
            var later = Create(0, id: "later");
            first.Completed += _ => { _system.Stop(later.Id); _system.Start(later.Id); };
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
            old.Started += t => { _system.DestroyTimer(t.Id); Create(0); _system.Start(t.Id); };
            _system.Start(old.Id);
            Tick(0, 0);
            Assert.That(old.State, Is.EqualTo(TimerState.Destroyed));
            Assert.That(_system.Get(old.Id).State, Is.EqualTo(TimerState.Completed));
        }

        [Test]
        public void CallbackErrors_KeepCommittedTransitionsAndDoNotStarveOtherTimers()
        {
            var first = Create(0);
            var second = Create(0, id: "second");
            first.Started += _ => throw new InvalidOperationException("start callback");
            first.Completed += _ => throw new InvalidOperationException("completion callback");
            Assert.Throws<InvalidOperationException>(() => _system.Start(first.Id));
            _system.Start(second.Id);
            var error = Assert.Throws<AggregateException>(() => Tick(0, 0));
            Assert.That(error.InnerExceptions.Count, Is.EqualTo(1));
            Assert.That(first.State, Is.EqualTo(TimerState.Completed));
            Assert.That(second.State, Is.EqualTo(TimerState.Completed));
            Assert.That(_active, Is.Empty);
        }
    }
}
