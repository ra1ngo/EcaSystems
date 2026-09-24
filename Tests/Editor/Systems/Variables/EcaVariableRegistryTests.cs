using System;
using System.Collections.Generic;
using EcaSystems.Variables;
using NUnit.Framework;

namespace EcaSystems.Tests.Variables
{
    public sealed class EcaVariableRegistryTests
    {
        [Test]
        public void DeclarationRegistersExactFacadeAndStoreProxiesReturnIt()
        {
            var store = new EcaVariablesSystem().CreateStore("store");
            var registry = store.Variables;
            Assert.That(registry.Variables, Is.Empty);
            var variable = store.Declare("id", 1);
            Assert.That(store.Variables, Is.SameAs(registry));
            Assert.That(registry.Contains("id"), Is.True);
            Assert.That(registry.Resolve("id"), Is.SameAs(variable));
            Assert.That(registry.TryResolve("id", out var found), Is.True);
            Assert.That(found, Is.SameAs(variable));
            Assert.That(store.GetVariable("id"), Is.SameAs(variable));
            Assert.That(store.TryGetVariable("id", out found), Is.True);
            Assert.That(found, Is.SameAs(variable));
            Assert.That(registry.Variables, Is.EquivalentTo(new[] { variable }));
        }

        [Test]
        public void RegistryIsLocalAndOrdinalAndDuplicatesPreserveOriginal()
        {
            var system = new EcaVariablesSystem();
            var parent = system.CreateStore("parent");
            var child = system.CreateStore("child", "parent");
            var original = parent.Declare("Money", 10);
            Assert.That(child.Variables, Is.Not.SameAs(parent.Variables));
            Assert.That(child.Variables.Contains("Money"), Is.False);
            Assert.That(parent.Variables.Contains("money"), Is.False);
            var lower = parent.Declare("money", "separate");
            Assert.Throws<InvalidOperationException>(() => parent.Declare("Money", 20));
            Assert.Throws<InvalidOperationException>(() => parent.Declare("Money", "wrong"));
            Assert.That(parent.Variables.Resolve("Money"), Is.SameAs(original));
            Assert.That(original.CurrentValue, Is.EqualTo(10));
            Assert.That(original.OldValue, Is.EqualTo(10));
            Assert.That(original.Definition.DefaultValue, Is.EqualTo(10));
            Assert.That(parent.Variables.Resolve("money"), Is.SameAs(lower));
            Assert.That(parent.Variables.Variables, Has.Count.EqualTo(2));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void RegistryRejectsInvalidIds(string id)
        {
            var registry = new EcaVariableRegistry();
            Assert.Throws<ArgumentException>(() => registry.Contains(id));
            Assert.Throws<ArgumentException>(() => registry.Resolve(id));
            Assert.Throws<ArgumentException>(() => registry.TryResolve(id, out _));
        }

        [Test]
        public void MissingRegistryLookupThrowsOrReturnsFalseAndNull()
        {
            var registry = new EcaVariableRegistry();
            Assert.That(registry.Contains("missing"), Is.False);
            Assert.Throws<InvalidOperationException>(() => registry.Resolve("missing"));
            Assert.That(registry.TryResolve("missing", out var variable), Is.False);
            Assert.That(variable, Is.Null);
        }

        [Test]
        public void CollectionCannotChangeMembershipAndStoreSnapshotCopiesValues()
        {
            var store = new EcaVariablesSystem().CreateStore("store");
            var collection = store.Variables.Variables;
            var variable = store.Declare("id", 1);
            Assert.That(collection, Is.EquivalentTo(new[] { variable }));
            var mutable = collection as ICollection<EcaVariable>;
            if (mutable != null)
            {
                Assert.That(mutable.IsReadOnly, Is.True);
                Assert.Throws<NotSupportedException>(() => mutable.Add(variable));
                Assert.Throws<NotSupportedException>(() => mutable.Remove(variable));
                Assert.Throws<NotSupportedException>(() => mutable.Clear());
            }
            var snapshot = store.GetStoreState();
            variable.SetValue(2);
            store.Declare("next", true);
            Assert.That(collection, Has.Count.EqualTo(2));
            Assert.That(snapshot.Variables, Has.Count.EqualTo(1));
            foreach (var item in snapshot.Variables)
            {
                Assert.That(item.Definition, Is.SameAs(variable.Definition));
                Assert.That(item.CurrentValue, Is.EqualTo(1));
            }
        }

        [TestCase("get")]
        [TestCase("set")]
        [TestCase("force")]
        [TestCase("current")]
        public void MandatoryProxyChecksIdThenExistenceThenSupportedThenExactType(string operation)
        {
            var store = new EcaVariablesSystem().CreateStore("store");
            var variable = store.Declare("id", 1);
            var events = 0;
            store.VariableChanged += _ => events++;
            Assert.Throws<ArgumentException>(() => Invoke<double>(store, operation, " "));
            var missing = Assert.Throws<InvalidOperationException>(() => Invoke<double>(store, operation, "missing"));
            Assert.That(missing.Message, Does.Contain("missing"));
            Assert.Throws<NotSupportedException>(() => Invoke<double>(store, operation, "id"));
            Assert.Throws<InvalidOperationException>(() => Invoke<float>(store, operation, "id"));
            Assert.That(variable.CurrentValue, Is.EqualTo(1));
            Assert.That(variable.OldValue, Is.EqualTo(1));
            Assert.That(events, Is.Zero);
        }

        private static void Invoke<T>(EcaVariableStore store, string operation, string id)
        {
            switch (operation)
            {
                case "get": store.GetValue<T>(id); break;
                case "set": store.SetValue<T>(id, default); break;
                case "force": store.ForceSetValue<T>(id, default); break;
                case "current": store.SetCurrentValue<T>(id, default); break;
                default: throw new ArgumentException(nameof(operation));
            }
        }

        [Test]
        public void TryGetValueSkipsTypeValidationOnlyWhenMissing()
        {
            var store = new EcaVariablesSystem().CreateStore("store");
            store.Declare("id", 7);
            Assert.Throws<ArgumentException>(() => store.TryGetValue<double>(null, out _));
            Assert.That(store.TryGetValue<double>("missing", out var missing), Is.False);
            Assert.That(missing, Is.Zero);
            Assert.Throws<NotSupportedException>(() => store.TryGetValue<double>("id", out _));
            Assert.Throws<InvalidOperationException>(() => store.TryGetValue<float>("id", out _));
            Assert.That(store.TryGetValue<int>("id", out var current), Is.True);
            Assert.That(current, Is.EqualTo(7));
        }

        [Test]
        public void DeclareValidatesSupportedTypeBeforeDuplicateRegistration()
        {
            var store = new EcaVariablesSystem().CreateStore("store");
            var original = store.Declare("id", 1);
            Assert.Throws<ArgumentException>(() => store.Declare<double>(null, 0));
            Assert.Throws<NotSupportedException>(() => store.Declare("new", 0d));
            Assert.Throws<NotSupportedException>(() => store.Declare("id", 0d));
            Assert.Throws<InvalidOperationException>(() => store.Declare("id", 2));
            Assert.That(store.Variables.Resolve("id"), Is.SameAs(original));
            Assert.That(store.Variables.Contains("new"), Is.False);
            Assert.That(store.Variables.Variables, Has.Count.EqualTo(1));
        }
    }
}
