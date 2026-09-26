using System;
using System.Collections.Generic;
using EcaSystems.Time;
using NUnit.Framework;

namespace EcaSystems.Tests.Time
{
    public sealed class RegistrySnapshotTests
    {
        [Test]
        public void Timers_SnapshotSurvivesAddRemoveAndRetainsLiveTimerData()
        {
            var registry = new TimerRegistry();
            var empty = registry.GetSnapshot();
            var first = new Timer(new TimerCreateOptions("first", 10)); registry.Register(first);
            var before = registry.GetSnapshot();
            var second = new Timer(new TimerCreateOptions("second", 5)); registry.Register(second);
            Assert.That(empty, Is.Empty);
            Assert.That(before.Count, Is.EqualTo(1));
            Assert.That(registry.GetSnapshot(), Is.EquivalentTo(new[] { first, second }));
            registry.Remove(first.Id);
            Assert.That(before[0], Is.SameAs(first));
            Assert.That(registry.GetSnapshot()[0], Is.SameAs(second));
            first.Elapsed = 3;
            Assert.That(before[0].Elapsed, Is.EqualTo(3));
            var collection = (ICollection<Timer>)before;
            Assert.That(collection.IsReadOnly, Is.True);
            Assert.Throws<NotSupportedException>(() => collection.Clear());
            Assert.Throws<NotSupportedException>(() => collection.Add(second));
        }

        [Test]
        public void Waits_SnapshotSurvivesCreateRemoveAndPreservesCompletionSource()
        {
            var registry = new WaitRegistry();
            var empty = registry.GetSnapshot();
            var first = registry.Create(10, TimerScaleMode.Scaled);
            var before = registry.GetSnapshot();
            var second = registry.Create(20, TimerScaleMode.Unscaled);
            Assert.That(empty, Is.Empty);
            Assert.That(before.Count, Is.EqualTo(1));
            Assert.That(registry.GetSnapshot(), Is.EquivalentTo(new[] { first, second }));
            registry.Remove(first);
            Assert.That(before[0], Is.SameAs(first));
            Assert.That(before[0].Completion, Is.SameAs(first.Completion));
            Assert.That(registry.GetSnapshot()[0], Is.SameAs(second));
            var collection = (ICollection<WaitTimer>)before;
            Assert.That(collection.IsReadOnly, Is.True);
            Assert.Throws<NotSupportedException>(() => collection.Clear());
            Assert.Throws<NotSupportedException>(() => collection.Add(second));
        }
    }
}
