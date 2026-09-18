using System.Collections.Generic;
using System.Threading.Tasks;
using EcaSystems.Time;
using NUnit.Framework;
using UnityEngine;

namespace EcaSystems.Tests.Time
{
    [TestFixture]
    public sealed class TimeWaitTests
    {
        private static async Task Observe(Awaitable wait) => await wait;

        [TestCase(TimerScaleMode.Scaled)]
        [TestCase(TimerScaleMode.Unscaled)]
        public void Wait_CompletesOnlyAtSelectedClockDeadline(TimerScaleMode mode)
        {
            var system = new TimeSystem(new TimeTicker());
            var observed = Observe(system.Wait(5, mode));
            system.Tick(mode == TimerScaleMode.Scaled ? 4 : 100, mode == TimerScaleMode.Unscaled ? 4 : 100);
            Assert.That(observed.IsCompleted, Is.False);
            system.Tick(5, 5);
            Assert.That(observed.IsCompletedSuccessfully, Is.True);
        }

        [Test]
        public void ZeroWait_CompletesOnNextTick_NotSynchronously()
        {
            var system = new TimeSystem(new TimeTicker());
            var observed = Observe(system.Wait(0));
            Assert.That(observed.IsCompleted, Is.False);
            system.Tick(0, 0);
            Assert.That(observed.IsCompletedSuccessfully, Is.True);
        }

        [Test]
        public void SimultaneousWaits_ShareRunnerButNotStateOrPublicIds()
        {
            var ticker = new TimeTicker();
            var system = new TimeSystem(ticker);
            var timer = system.CreateTimer(new TimerCreateOptions("wait-1", 10));
            var first = Observe(system.Wait(1));
            var second = Observe(system.Wait(2));
            var unscaled = Observe(system.Wait(3, TimerScaleMode.Unscaled));
            Assert.That(system.Get("wait-1"), Is.SameAs(timer));
            Assert.That(system.TryGet("wait-2", out _), Is.False);
            Assert.That(ticker.SubscriberCount, Is.EqualTo(1));
            system.Tick(1, 0);
            Assert.That(first.IsCompletedSuccessfully, Is.True);
            Assert.That(second.IsCompleted, Is.False);
            Assert.That(unscaled.IsCompleted, Is.False);
            system.Tick(2, 3);
            Assert.That(second.IsCompletedSuccessfully, Is.True);
            Assert.That(unscaled.IsCompletedSuccessfully, Is.True);
            Assert.That(ticker.SubscriberCount, Is.Zero);
            Assert.That(timer.State, Is.EqualTo(TimerState.Stopped));
        }

        [Test]
        public void AwaitContinuation_CanScheduleAnotherWait()
        {
            var system = new TimeSystem(new TimeTicker());
            var stages = 0;
            async Task Sequence()
            {
                await system.Wait(0);
                stages++;
                await system.Wait(0);
                stages++;
            }
            var sequence = Sequence();
            system.Tick(0, 0);
            Assert.That(stages, Is.EqualTo(1));
            Assert.That(sequence.IsCompleted, Is.False);
            system.Tick(0, 0);
            Assert.That(stages, Is.EqualTo(2));
            Assert.That(sequence.IsCompletedSuccessfully, Is.True);
        }
    }
}
