using System;
using EcaSystems.Time;
using NUnit.Framework;
using UnityEngine.LowLevel;

namespace EcaSystems.Tests.Time
{
    [TestFixture]
    public sealed class TimePlayerLoopTests
    {
        private PlayerLoopSystem _original;

        [SetUp]
        public void SetUp() => _original = PlayerLoop.GetCurrentPlayerLoop();

        [TearDown]
        public void TearDown() => PlayerLoop.SetPlayerLoop(_original);

        [Test]
        public void Install_IsIdempotentAndPreservesOtherUpdateEntries()
        {
            var before = Count(_original, typeof(UnityEngine.PlayerLoop.Update.ScriptRunBehaviourUpdate));
            TimeSystemPlayerLoop.Install();
            TimeSystemPlayerLoop.Install();
            var installed = PlayerLoop.GetCurrentPlayerLoop();
            Assert.That(Count(installed, typeof(TimeSystemPlayerLoop)), Is.EqualTo(1));
            Assert.That(Count(installed, typeof(UnityEngine.PlayerLoop.Update.ScriptRunBehaviourUpdate)), Is.EqualTo(before));
            Assert.That(Find(Find(installed, typeof(UnityEngine.PlayerLoop.Update)), typeof(TimeSystemPlayerLoop)).updateDelegate, Is.Not.Null);
        }

        [Test]
        public void InstalledDelegate_TicksIndependentPublicInstancesAndWait()
        {
            var first = new TimeSystem();
            var second = new TimeSystem();
            var a = first.CreateTimer(new TimerCreateOptions("same", 0));
            var b = second.CreateTimer(new TimerCreateOptions("same", 0, TimerScaleMode.Unscaled));
            try
            {
                first.Start(a.Id);
                second.Start(b.Id);
                var wait = first.Wait(0).GetAwaiter();
                Assert.That(wait.IsCompleted, Is.False);
                var hook = Find(PlayerLoop.GetCurrentPlayerLoop(), typeof(TimeSystemPlayerLoop));
                hook.updateDelegate();
                Assert.That(a.State, Is.EqualTo(TimerState.Completed));
                Assert.That(b.State, Is.EqualTo(TimerState.Completed));
                Assert.That(wait.IsCompleted, Is.True);
                wait.GetResult();
            }
            finally
            {
                first.DestroyTimer(a.Id);
                second.DestroyTimer(b.Id);
            }
        }

        [Test]
        public void WarmInstalledDelegate_DoesNotAllocateAtSameOrSmallerRunnerCount()
        {
            var systems = new TimeSystem[8];
            try
            {
                for (var i = 0; i < systems.Length; i++)
                {
                    systems[i] = new TimeSystem();
                    systems[i].CreateTimer(new TimerCreateOptions("timer", 1000000));
                    systems[i].Start("timer");
                }
                var update = Find(PlayerLoop.GetCurrentPlayerLoop(), typeof(TimeSystemPlayerLoop)).updateDelegate;
                for (var i = 0; i < 10; i++) update();
                var before = GC.GetAllocatedBytesForCurrentThread();
                for (var i = 0; i < 100; i++) update();
                var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.That(allocated, Is.Zero);
                for (var i = 1; i < systems.Length; i++) systems[i].Stop("timer");
                before = GC.GetAllocatedBytesForCurrentThread();
                for (var i = 0; i < 100; i++) update();
                allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.That(allocated, Is.Zero);
            }
            finally
            {
                foreach (var system in systems)
                    if (system != null && system.TryGet("timer", out _)) system.DestroyTimer("timer");
            }
        }

        [Test]
        public void NestedInstalledDelegate_PreservesOuterIteration()
        {
            var first = new TimeSystem();
            var second = new TimeSystem();
            var a = first.CreateTimer(new TimerCreateOptions("timer", 0));
            var b = second.CreateTimer(new TimerCreateOptions("timer", 0));
            var completions = 0;
            PlayerLoopSystem.UpdateFunction update = null;
            a.Completed += _ => { completions++; update(); };
            b.Completed += _ => { completions++; update(); };
            try
            {
                first.Start(a.Id);
                second.Start(b.Id);
                update = Find(PlayerLoop.GetCurrentPlayerLoop(), typeof(TimeSystemPlayerLoop)).updateDelegate;
                update();
                Assert.That(completions, Is.EqualTo(2));
                Assert.That(a.State, Is.EqualTo(TimerState.Completed));
                Assert.That(b.State, Is.EqualTo(TimerState.Completed));
            }
            finally
            {
                first.DestroyTimer(a.Id);
                second.DestroyTimer(b.Id);
            }
        }

        private static int Count(PlayerLoopSystem loop, Type type)
        {
            var count = loop.type == type ? 1 : 0;
            if (loop.subSystemList != null)
                foreach (var child in loop.subSystemList) count += Count(child, type);
            return count;
        }

        private static PlayerLoopSystem Find(PlayerLoopSystem loop, Type type)
        {
            if (loop.type == type) return loop;
            if (loop.subSystemList != null)
                foreach (var child in loop.subSystemList)
                {
                    var found = Find(child, type);
                    if (found.type == type) return found;
                }
            return default;
        }
    }
}
