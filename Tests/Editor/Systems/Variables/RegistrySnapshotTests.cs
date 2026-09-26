using System;
using System.Collections.Generic;
using EcaSystems.Variables;
using NUnit.Framework;

namespace EcaSystems.Tests.Variables
{
    public sealed class RegistrySnapshotTests
    {
        [Test]
        public void SnapshotFreezesLocalMembershipButRetainsMutableFacades()
        {
            var store = new EcaVariablesSystem().CreateStore("store");
            var empty = store.Variables.GetSnapshot();
            var first = store.Declare("first", 1);
            var before = store.Variables.GetSnapshot();
            var second = store.Declare("second", 2);
            Assert.That(empty, Is.Empty);
            Assert.That(before.Count, Is.EqualTo(1));
            Assert.That(before[0], Is.SameAs(first));
            Assert.That(store.Variables.GetSnapshot(), Is.EquivalentTo(new[] { first, second }));
            first.SetValue(3);
            Assert.That(before[0].GetValue<int>(), Is.EqualTo(3));
            var collection = (ICollection<EcaVariable>)before;
            Assert.That(collection.IsReadOnly, Is.True);
            Assert.Throws<NotSupportedException>(() => collection.Clear());
            Assert.Throws<NotSupportedException>(() => collection.Add(second));
            Assert.That(store.GetVariable("first"), Is.SameAs(first));
            // Variables deliberately has no removal API; no new API is introduced for this test.
        }
    }
}
