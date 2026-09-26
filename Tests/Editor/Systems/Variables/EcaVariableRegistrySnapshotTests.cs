using System;
using System.Collections.Generic;
using EcaSystems.Variables;
using NUnit.Framework;

namespace EcaSystems.Tests.Variables
{
    public sealed class EcaVariableRegistrySnapshotTests
    {
        [Test]
        public void SnapshotCopiesMembershipWhileKeepingLiveFacadeInstances()
        {
            var store = new EcaVariablesSystem().CreateStore("store");
            var variable = store.Declare("first", 1);
            var snapshot = store.Variables.GetSnapshot();
            Assert.That(snapshot, Is.EqualTo(new[] { variable }));
            Assert.Throws<NotSupportedException>(() => ((IList<EcaVariable>)snapshot).Clear());
            store.Declare("second", false); variable.SetValue(2);
            Assert.That(snapshot, Is.EqualTo(new[] { variable }));
            Assert.That(snapshot[0].CurrentValue, Is.EqualTo(2), "Registry snapshot copies membership, not external state.");
            Assert.That(store.Variables.GetSnapshot(), Has.Count.EqualTo(2));
        }
    }
}
