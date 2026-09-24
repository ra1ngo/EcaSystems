using System;
using System.Collections.Generic;
using System.Linq;
using EcaSystems.Variables;
using NUnit.Framework;

namespace EcaSystems.Tests.Variables
{
    public sealed class EcaVariablesSystemStateTests
    {
        [Test]
        public void EmptySystemReturnsEmptyReadOnlySnapshot()
        {
            var system = new EcaVariablesSystem();
            var state = system.GetState();
            Assert.That(state.Stores, Is.Empty);
            Assert.Throws<NotSupportedException>(() => ((ICollection<EcaVariableStoreState>)state.Stores).Clear());
            system.CreateStore("later");
            Assert.That(state.Stores, Is.Empty);
            Assert.That(system.GetState().Stores, Has.Count.EqualTo(1));
        }

        [Test]
        public void ForestSnapshotIncludesEveryStoreOnceWithTopologyAndLocalVariables()
        {
            var system = new EcaVariablesSystem();
            var root = system.CreateStore("root");
            var child = system.CreateStore("child", "root");
            system.CreateStore("leaf", "child");
            system.CreateStore("other");
            root.Declare("only-parent", 1);
            child.Declare("local", true);
            var state = system.GetState();
            Assert.That(state.Stores.Select(s => s.StoreId), Is.EquivalentTo(new[] { "root", "child", "leaf", "other" }));
            var stores = state.Stores.ToDictionary(s => s.StoreId);
            Assert.That(stores["root"].ParentId, Is.Null);
            Assert.That(stores["root"].IsRoot, Is.True);
            Assert.That(stores["other"].IsRoot, Is.True);
            Assert.That(stores["child"].ParentId, Is.EqualTo("root"));
            Assert.That(stores["child"].IsRoot, Is.False);
            Assert.That(stores["leaf"].ParentId, Is.EqualTo("child"));
            Assert.That(stores["root"].Variables.Select(v => v.Definition.Id), Is.EquivalentTo(new[] { "only-parent" }));
            Assert.That(stores["child"].Variables.Select(v => v.Definition.Id), Is.EquivalentTo(new[] { "local" }));
            Assert.That(stores["leaf"].Variables, Is.Empty);
            Assert.That(stores["other"].Variables, Is.Empty);
            Assert.That(stores["child"].Variables.Single().StoreId, Is.EqualTo("child"));
        }

        [TestCase("set")]
        [TestCase("force")]
        [TestCase("current")]
        public void SnapshotCopiesValuesBeforeMutationDeclarationAndStoreCreation(string operation)
        {
            var system = new EcaVariablesSystem();
            var store = system.CreateStore("root");
            var variable = store.Declare("v", 1);
            var state = system.GetState();
            switch (operation)
            {
                case "set": variable.SetValue(2); break;
                case "force": variable.ForceSetValue(2); break;
                case "current": variable.SetCurrentValue(2); break;
            }
            store.Declare("later", false);
            system.CreateStore("later", "root");
            Assert.That(state.Stores, Has.Count.EqualTo(1));
            var snapshot = state.Stores.Single().Variables.Single();
            Assert.That(snapshot.CurrentValue, Is.EqualTo(1));
            Assert.That(snapshot.OldValue, Is.EqualTo(1));
            Assert.That(snapshot.Definition, Is.SameAs(variable.Definition));
            Assert.That(snapshot.Definition.DefaultValue, Is.EqualTo(1));
            Assert.Throws<NotSupportedException>(() => ((IList<EcaVariableStoreState>)state.Stores)[0] = null);
            Assert.Throws<NotSupportedException>(() => ((ICollection<EcaVariableSnapshot>)state.Stores[0].Variables).Clear());
            var current = system.GetState();
            Assert.That(current.Stores, Has.Count.EqualTo(2));
            Assert.That(current.Stores.Single(s => s.StoreId == "root").Variables.Single(v => v.Definition.Id == "v").CurrentValue, Is.EqualTo(2));
        }
    }
}
