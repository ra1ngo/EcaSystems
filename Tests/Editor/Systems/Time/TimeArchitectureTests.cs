using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using EcaSystems.Time;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EcaSystems.Tests.Time
{
    public sealed class TimeArchitectureTests
    {
        [Test]
        public void Subscription_FollowsRegistryAndWaitWork()
        {
            var ticker = new TimeTicker();
            var system = new TimeSystem(ticker);
            var timer = system.CreateTimer(new TimerCreateOptions("timer", 2));
            Assert.That(ticker.SubscriberCount, Is.Zero);
            Assert.That(timer.IsActive, Is.False);
            system.Start(timer.Id);
            Assert.That(ticker.SubscriberCount, Is.EqualTo(1));
            Assert.That(timer.IsActive, Is.True);
            system.Pause(timer.Id);
            Assert.That(ticker.SubscriberCount, Is.Zero);
            var wait = system.Wait(3).GetAwaiter();
            system.Resume(timer.Id);
            system.Stop(timer.Id);
            Assert.That(ticker.SubscriberCount, Is.EqualTo(1));
            ticker.Publish(3, 3);
            Assert.That(wait.IsCompleted, Is.True);
            wait.GetResult();
            Assert.That(ticker.SubscriberCount, Is.Zero);
            system.Start(timer.Id);
            system.DestroyTimer(timer.Id);
            Assert.That(ticker.SubscriberCount, Is.Zero);
        }

        [Test]
        public void Ticker_UpdatesBothClocksBeforeCallbacks_AndIsolatesFailures()
        {
            var ticker = new TimeTicker();
            ticker.TimeTick += (_, __) => throw new InvalidOperationException("broken subscriber");
            var system = new TimeSystem(ticker);
            var timer = system.CreateTimer(new TimerCreateOptions("timer", 2));
            system.Start(timer.Id);
            var seen = false;
            ticker.TimeTick += (scaled, unscaled) =>
            {
                seen = true;
                Assert.That(ticker.CurrentTime, Is.EqualTo(scaled));
                Assert.That(ticker.CurrentUnscaledTime, Is.EqualTo(unscaled));
            };
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: broken subscriber"));
            ticker.Publish(2, 7);
            Assert.That(timer.State, Is.EqualTo(TimerState.Completed));
            Assert.That(seen, Is.True);
        }

        [Test]
        public void TimerFailure_DoesNotStarveWaitOrAnotherSystem()
        {
            var ticker = new TimeTicker();
            var first = new TimeSystem(ticker);
            var second = new TimeSystem(ticker);
            first.CreateTimer(new TimerCreateOptions("a", 0));
            var b = second.CreateTimer(new TimerCreateOptions("b", 0));
            first.TimerCompleted += _ => throw new InvalidOperationException("completion failure");
            first.Start("a");
            second.Start("b");
            var wait = first.Wait(0).GetAwaiter();
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: completion failure"));
            ticker.Publish(0, 0);
            Assert.That(wait.IsCompleted, Is.True);
            wait.GetResult();
            Assert.That(b.State, Is.EqualTo(TimerState.Completed));
            Assert.That(ticker.SubscriberCount, Is.Zero);
        }

        [Test]
        public void TimerCallback_NewWaitAndNewTimerWaitForNextTick()
        {
            var ticker = new TimeTicker();
            var system = new TimeSystem(ticker);
            system.CreateTimer(new TimerCreateOptions("a", 0));
            Task wait = null;
            async Task Observe() => await system.Wait(0);
            system.TimerCompleted += timer =>
            {
                if (timer.Id != "a") return;
                system.DestroyTimer(timer.Id);
                system.CreateTimer(new TimerCreateOptions("b", 0));
                system.Start("b");
                wait = Observe();
            };
            system.Start("a");
            ticker.Publish(0, 0);
            Assert.That(wait.IsCompleted, Is.False);
            Assert.That(system.Get("b").IsActive, Is.True);
            ticker.Publish(0, 0);
            Assert.That(wait.IsCompletedSuccessfully, Is.True);
            Assert.That(system.Get("b").State, Is.EqualTo(TimerState.Completed));
            Assert.That(ticker.SubscriberCount, Is.Zero);
        }

        [Test]
        public void Scan_UpdatesAllRunningValuesBeforeCompletionCallbacks()
        {
            var ticker = new TimeTicker();
            var system = new TimeSystem(ticker);
            system.CreateTimer(new TimerCreateOptions("due", 1));
            var later = system.CreateTimer(new TimerCreateOptions("later", 4));
            var idle = system.CreateTimer(new TimerCreateOptions("idle", 1));
            system.TimerCompleted += _ =>
            {
                Assert.That(later.Elapsed, Is.EqualTo(1));
                Assert.That(later.Progress, Is.EqualTo(.25));
                Assert.That(idle.Elapsed, Is.Zero);
                system.DestroyTimer(later.Id);
            };
            system.Start("due");
            system.Start("later");
            ticker.Publish(1, 1);
            Assert.That(later.State, Is.EqualTo(TimerState.Destroyed));
            Assert.That(ticker.SubscriberCount, Is.Zero);
        }

        [Test]
        public void PauseAndDestroy_CommitTickerTimeBetweenProcessingTicks()
        {
            var ticker = new TimeTicker();
            var system = new TimeSystem(ticker);
            var a = system.CreateTimer(new TimerCreateOptions("a", 10));
            var b = system.CreateTimer(new TimerCreateOptions("b", 10, TimerScaleMode.Unscaled));
            system.Start("a");
            system.Start("b");
            ticker.UpdateTime(3, 6);
            Assert.That(a.Elapsed, Is.Zero);
            system.Pause("a");
            system.DestroyTimer("b");
            Assert.That(a.Elapsed, Is.EqualTo(3));
            Assert.That(b.Elapsed, Is.EqualTo(6));
            Assert.That(ticker.SubscriberCount, Is.Zero);
        }

        [Test]
        public void WaitContinuationFailure_DoesNotStarveOtherDueWait()
        {
            var ticker = new TimeTicker();
            var system = new TimeSystem(ticker);
            var first = system.Wait(0).GetAwaiter();
            var second = system.Wait(0).GetAwaiter();
            first.OnCompleted(() =>
            {
                first.GetResult();
                throw new InvalidOperationException("wait continuation failure");
            });
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: wait continuation failure"));
            ticker.Publish(0, 0);
            Assert.That(second.IsCompleted, Is.True);
            second.GetResult();
            Assert.That(ticker.SubscriberCount, Is.Zero);
        }
    }
}
