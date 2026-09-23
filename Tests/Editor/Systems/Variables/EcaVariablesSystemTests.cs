using System;
using EcaSystems.Variables;
using NUnit.Framework;

namespace EcaSystems.Tests.Variables
{
    public sealed class EcaVariablesSystemTests
    {
        [Test]
        public void CreateStoreBuildsForestWithImmutableCoordinatesAndNoImplicitRoot()
        {
            var system = new EcaVariablesSystem();
            Assert.That(system.ContainsStore("global"), Is.False);
            Assert.That(system.ContainsStore("default"), Is.False);
            Assert.That(system.ContainsStore("root"), Is.False);
            var root = system.CreateStore("root");
            var other = system.CreateStore("other");
            var child = system.CreateStore("child", "root");
            var grandchild = system.CreateStore("grandchild", "child");
            Assert.That(root.Id, Is.EqualTo("root"));
            Assert.That(root.ParentId, Is.Null);
            Assert.That(other.ParentId, Is.Null);
            Assert.That(child.Id, Is.EqualTo("child"));
            Assert.That(child.ParentId, Is.EqualTo("root"));
            Assert.That(grandchild.Id, Is.EqualTo("grandchild"));
            Assert.That(grandchild.ParentId, Is.EqualTo("child"));
            foreach (var store in new[] { root, other, child, grandchild })
            {
                Assert.That(system.ContainsStore(store.Id), Is.True);
                Assert.That(system.GetStore(store.Id), Is.SameAs(store));
                Assert.That(system.TryGetStore(store.Id, out var found), Is.True);
                Assert.That(found, Is.SameAs(store));
            }
        }

        [Test]
        public void StoreIdsAreGloballyUniqueAcrossBranchesButIndependentBetweenSystems()
        {
            var system = new EcaVariablesSystem();
            system.CreateStore("a");
            system.CreateStore("b");
            var child = system.CreateStore("child", "a");
            Assert.Throws<InvalidOperationException>(() => system.CreateStore("child", "b"));
            Assert.Throws<InvalidOperationException>(() => system.CreateStore("child"));
            Assert.Throws<InvalidOperationException>(() => system.CreateStore("a", "b"));
            Assert.That(system.GetStore("child"), Is.SameAs(child));
            Assert.That(child.ParentId, Is.EqualTo("a"));
            var independent = new EcaVariablesSystem().CreateStore("child");
            Assert.That(independent, Is.Not.SameAs(child));
            Assert.That(independent.ParentId, Is.Null);
        }

        [Test]
        public void IdsAreOrdinalOpaqueStringsNotPaths()
        {
            var system = new EcaVariablesSystem();
            var upper = system.CreateStore("Root");
            var lower = system.CreateStore("root");
            var path = system.CreateStore("Root/child");
            Assert.That(system.GetStore("Root"), Is.SameAs(upper));
            Assert.That(system.GetStore("root"), Is.SameAs(lower));
            Assert.That(path.ParentId, Is.Null);
            Assert.That(system.ContainsStore("ROOT"), Is.False);
            Assert.Throws<InvalidOperationException>(() => system.CreateStore("child", "ROOT"));
            Assert.That(system.ContainsStore("child"), Is.False);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void InvalidStoreIdsAreRejectedByEveryManagerOperation(string id)
        {
            var system = new EcaVariablesSystem();
            Assert.Throws<ArgumentException>(() => system.CreateStore(id));
            Assert.Throws<ArgumentException>(() => system.ContainsStore(id));
            Assert.Throws<ArgumentException>(() => system.GetStore(id));
            Assert.Throws<ArgumentException>(() => system.TryGetStore(id, out _));
        }

        [TestCase("")]
        [TestCase(" ")]
        public void InvalidParentIdsDoNotReserveChildId(string parentId)
        {
            var system = new EcaVariablesSystem();
            Assert.Throws<ArgumentException>(() => system.CreateStore("child", parentId));
            Assert.That(system.ContainsStore("child"), Is.False);
            Assert.That(system.CreateStore("child").ParentId, Is.Null);
        }

        [Test]
        public void MissingParentDoesNotReserveChildIdAndCanBeCreatedLater()
        {
            var system = new EcaVariablesSystem();
            Assert.Throws<InvalidOperationException>(() => system.CreateStore("child", "parent"));
            Assert.That(system.ContainsStore("child"), Is.False);
            Assert.That(system.ContainsStore("parent"), Is.False);
            system.CreateStore("parent");
            Assert.That(system.CreateStore("child", "parent").ParentId, Is.EqualTo("parent"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SelfParentFailsWithoutCreatingOrChangingStore(bool alreadyExists)
        {
            var system = new EcaVariablesSystem();
            var original = alreadyExists ? system.CreateStore("a") : null;
            var error = Assert.Throws<InvalidOperationException>(() => system.CreateStore("a", "a"));
            Assert.That(error.Message, Does.Contain(alreadyExists ? "already exists" : "does not exist"));
            Assert.That(system.ContainsStore("a"), Is.EqualTo(alreadyExists));
            if (alreadyExists)
            {
                Assert.That(system.GetStore("a"), Is.SameAs(original));
                Assert.That(original.ParentId, Is.Null);
            }
        }

        [Test]
        public void MissingStoreLookupReturnsFalseAndNullOrThrows()
        {
            var system = new EcaVariablesSystem();
            Assert.That(system.ContainsStore("missing"), Is.False);
            Assert.Throws<InvalidOperationException>(() => system.GetStore("missing"));
            Assert.That(system.TryGetStore("missing", out var store), Is.False);
            Assert.That(store, Is.Null);
        }

        [Test]
        public void ChildAndGrandchildNeverReadOrMutateParentVariables()
        {
            var system = new EcaVariablesSystem();
            var parent = system.CreateStore("parent");
            var child = system.CreateStore("child", "parent");
            var grandchild = system.CreateStore("grandchild", "child");
            var original = parent.Declare("difficulty", 2);
            var notifications = 0;
            parent.VariableChanged += _ => notifications++;
            child.VariableChanged += _ => notifications++;
            grandchild.VariableChanged += _ => notifications++;
            foreach (var local in new[] { child, grandchild })
            {
                Assert.That(local.Contains("difficulty"), Is.False);
                Assert.That(local.TryGetValue<int>("difficulty", out var value), Is.False);
                Assert.That(value, Is.Zero);
                Assert.Throws<InvalidOperationException>(() => local.GetValue<int>("difficulty"));
                Assert.Throws<InvalidOperationException>(() => local.SetValue("difficulty", 3));
                Assert.Throws<InvalidOperationException>(() => local.ForceSetValue("difficulty", 3));
                Assert.Throws<InvalidOperationException>(() => local.SetCurrentValue("difficulty", 3));
            }
            Assert.That(original.CurrentValue, Is.EqualTo(2));
            Assert.That(original.OldValue, Is.EqualTo(2));
            Assert.That(original.Definition.DefaultValue, Is.EqualTo(2));
            Assert.That(notifications, Is.Zero);
        }

        [Test]
        public void SameVariableIdIsIndependentAcrossParentsChildrenSiblingsAndRoots()
        {
            var system = new EcaVariablesSystem();
            var parent = system.CreateStore("parent");
            var child = system.CreateStore("child", "parent");
            var sibling = system.CreateStore("sibling", "parent");
            var root = system.CreateStore("root");
            var parentValue = parent.Declare("id", 1);
            var childValue = child.Declare("id", 2);
            var siblingValue = sibling.Declare("id", "different contract");
            var rootValue = root.Declare("id", 4);
            var parentEvents = 0;
            var childEvents = 0;
            var siblingEvents = 0;
            var rootEvents = 0;
            parent.VariableChanged += _ => parentEvents++;
            child.VariableChanged += _ => childEvents++;
            sibling.VariableChanged += _ => siblingEvents++;
            root.VariableChanged += _ => rootEvents++;
            child.SetValue("id", 20);
            child.ForceSetValue("id", 20);
            child.SetCurrentValue("id", 30);
            Assert.That(childValue.CurrentValue, Is.EqualTo(30));
            Assert.That(childValue.OldValue, Is.EqualTo(20));
            Assert.That(childValue.Definition, Is.Not.SameAs(parentValue.Definition));
            Assert.That(parentValue.CurrentValue, Is.EqualTo(1));
            Assert.That(siblingValue.CurrentValue, Is.EqualTo("different contract"));
            Assert.That(rootValue.CurrentValue, Is.EqualTo(4));
            Assert.That(childEvents, Is.EqualTo(2));
            Assert.That(parentEvents, Is.Zero);
            Assert.That(siblingEvents, Is.Zero);
            Assert.That(rootEvents, Is.Zero);
            parent.SetValue("id", 10);
            Assert.That(parentEvents, Is.EqualTo(1));
            Assert.That(childEvents, Is.EqualTo(2));
            Assert.That(child.GetValue<int>("id"), Is.EqualTo(30));
        }
    }
}
