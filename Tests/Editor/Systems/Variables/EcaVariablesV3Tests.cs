using System;
using System.Collections.Generic;
using System.Linq;
using EcaSystems.Variables;
using NUnit.Framework;

namespace EcaSystems.Tests.Variables
{
    public sealed class EcaVariablesV3Tests
    {
        [Test] public void IntFacadeUsesLiveData() => VerifyFacade(0, 1, 2, 3);
        [Test] public void FloatFacadeUsesLiveData() => VerifyFacade(0f, 1.5f, 2.5f, 3.5f);
        [Test] public void BoolFacadeUsesLiveData() => VerifyFacade(false, true, false, true);
        [Test] public void StringFacadeUsesLiveData() => VerifyFacade<string>(null, "first", "direct", "last");

        private static void VerifyFacade<T>(T initial, T changed, T direct, T last)
        {
            var system = new EcaVariablesSystem();
            var store = system.CreateStore("store");
            var variable = store.Declare("v", initial);
            var data = variable.Data;
            var events = new List<EcaVariableChanged>();
            system.VariableChanged += events.Add;
            Assert.That(variable.StoreId, Is.EqualTo(store.Id));
            Assert.That(data.StoreId, Is.EqualTo(store.Id));
            Assert.That(variable.Definition, Is.SameAs(data.Definition));
            Assert.That(data.CurrentValue, Is.EqualTo(initial));
            Assert.That(data.OldValue, Is.EqualTo(initial));
            Assert.That(variable.GetValue<T>(), Is.EqualTo(initial));
            variable.SetValue(changed);
            Assert.That(variable.Data, Is.SameAs(data));
            Assert.That(data.CurrentValue, Is.EqualTo(changed));
            Assert.That(variable.CurrentValue, Is.EqualTo(data.CurrentValue));
            Assert.That(variable.OldValue, Is.EqualTo(data.OldValue));
            Assert.That(data.OldValue, Is.EqualTo(initial));
            Assert.That(events, Has.Count.EqualTo(1));
            variable.SetValue(changed);
            Assert.That(events, Has.Count.EqualTo(1));
            Assert.That(data.OldValue, Is.EqualTo(initial));
            variable.ForceSetValue(changed);
            Assert.That(events, Has.Count.EqualTo(2));
            Assert.That(data.OldValue, Is.EqualTo(changed));
            variable.SetCurrentValue(direct);
            Assert.That(events, Has.Count.EqualTo(2));
            Assert.That(variable.GetValue<T>(), Is.EqualTo(direct));
            Assert.That(store.GetValue<T>("v"), Is.EqualTo(direct));
            Assert.That(data.OldValue, Is.EqualTo(changed));
            store.SetValue("v", last);
            Assert.That(variable.GetValue<T>(), Is.EqualTo(last));
            Assert.That(data.OldValue, Is.EqualTo(direct));
            Assert.That(events, Has.Count.EqualTo(3));
            Assert.That(variable.Definition.DefaultValue, Is.EqualTo(initial));
            Assert.That(events[0].StoreId, Is.EqualTo(store.Id));
            Assert.That((object)events[0], Is.Not.SameAs(variable));
            Assert.That((object)events[0], Is.Not.SameAs(data));
            Assert.That(events[0].Definition, Is.SameAs(variable.Definition));
            Assert.That(events[0].CurrentValue, Is.EqualTo(changed));
            Assert.That(events[0].OldValue, Is.EqualTo(initial));
        }

        [Test]
        public void DirectValidationDoesNotMutateOrPublish()
        {
            var system = new EcaVariablesSystem();
            var variable = system.CreateStore("store").Declare("v", 1);
            variable.SetValue(2);
            var events = 0;
            system.VariableChanged += _ => events++;
            Assert.Throws<InvalidOperationException>(() => variable.GetValue<float>());
            Assert.Throws<InvalidOperationException>(() => variable.SetValue(3f));
            Assert.Throws<InvalidOperationException>(() => variable.ForceSetValue(3f));
            Assert.Throws<InvalidOperationException>(() => variable.SetCurrentValue(3f));
            Assert.Throws<NotSupportedException>(() => variable.GetValue<object>());
            Assert.Throws<NotSupportedException>(() => variable.SetValue(3d));
            Assert.Throws<NotSupportedException>(() => variable.ForceSetValue(3d));
            Assert.Throws<NotSupportedException>(() => variable.SetCurrentValue(3d));
            Assert.That(variable.CurrentValue, Is.EqualTo(2));
            Assert.That(variable.OldValue, Is.EqualTo(1));
            Assert.That(events, Is.Zero);
        }

        [Test]
        public void VariableLookupReturnsExactFacadeAndRemainsLocal()
        {
            var system = new EcaVariablesSystem();
            var root = system.CreateStore("root");
            var child = system.CreateStore("child", "root");
            var variable = root.Declare("v", 1);
            Assert.That(root.GetVariable("v"), Is.SameAs(variable));
            Assert.That(root.TryGetVariable("v", out var found), Is.True);
            Assert.That(found, Is.SameAs(variable));
            Assert.Throws<InvalidOperationException>(() => child.GetVariable("v"));
            Assert.That(child.TryGetVariable("v", out found), Is.False);
            Assert.That(found, Is.Null);
            Assert.Throws<InvalidOperationException>(() => root.GetVariable("missing"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void VariableLookupRejectsInvalidId(string id)
        {
            var store = new EcaVariablesSystem().CreateStore("store");
            Assert.Throws<ArgumentException>(() => store.GetVariable(id));
            Assert.Throws<ArgumentException>(() => store.TryGetVariable(id, out _));
        }

        [Test]
        public void NavigationUsesExactInstancesAndRootSiblingsWithoutTraversalOrderContract()
        {
            var system = new EcaVariablesSystem();
            var a = system.CreateStore("A");
            Assert.That(a.GetSiblings(), Is.Empty);
            var b = system.CreateStore("B", "A");
            var c = system.CreateStore("C", "A");
            var d = system.CreateStore("D", "B");
            var e = system.CreateStore("E", "B");
            var x = system.CreateStore("X");
            Assert.That(a.IsRoot, Is.True);
            Assert.That(x.IsRoot, Is.True);
            Assert.That(b.IsRoot, Is.False);
            Assert.That(d.IsRoot, Is.False);
            Assert.That(a.GetParent(), Is.Null);
            Assert.That(a.TryGetParent(out var parent), Is.False);
            Assert.That(parent, Is.Null);
            Assert.That(b.GetParent(), Is.SameAs(a));
            Assert.That(d.TryGetParent(out parent), Is.True);
            Assert.That(parent, Is.SameAs(b));
            Assert.That(a.GetChildren(), Is.EquivalentTo(new[] { b, c }));
            Assert.That(b.GetChildren(), Is.EquivalentTo(new[] { d, e }));
            Assert.That(d.GetChildren(), Is.Empty);
            Assert.That(a.GetSiblings(), Is.EquivalentTo(new[] { x }));
            Assert.That(x.GetSiblings(), Is.EquivalentTo(new[] { a }));
            Assert.That(b.GetSiblings(), Is.EquivalentTo(new[] { c }));
            Assert.That(d.GetSiblings(), Is.EquivalentTo(new[] { e }));
            Assert.That(a.GetSubtree(), Is.EquivalentTo(new[] { a, b, c, d, e }));
            Assert.That(b.GetSubtree(), Is.EquivalentTo(new[] { b, d, e }));
            Assert.That(d.GetSubtree(), Is.EquivalentTo(new[] { d }));
        }

        [Test]
        public void NavigationCollectionsAreReadonlyMembershipSnapshots()
        {
            var system = new EcaVariablesSystem();
            var root = system.CreateStore("root");
            var child = system.CreateStore("child", "root");
            var children = root.GetChildren();
            var subtree = root.GetSubtree();
            var siblings = root.GetSiblings();
            system.CreateStore("later", "child");
            system.CreateStore("otherChild", "root");
            system.CreateStore("otherRoot");
            Assert.That(children, Is.EquivalentTo(new[] { child }));
            Assert.That(subtree, Is.EquivalentTo(new[] { root, child }));
            Assert.That(siblings, Is.Empty);
            Assert.Throws<NotSupportedException>(() => ((IList<EcaVariableStore>)children).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<EcaVariableStore>)subtree).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<EcaVariableStore>)siblings).Add(root));
        }

        [Test]
        public void StoreStateCopiesLocalVariablesAndKeepsValuesAndCollectionAfterMutation()
        {
            var system = new EcaVariablesSystem();
            var root = system.CreateStore("root");
            var store = system.CreateStore("store", "root");
            var child = system.CreateStore("child", "store");
            root.Declare("parentOnly", 1);
            child.Declare("childOnly", 2);
            var variable = store.Declare("v", 0);
            variable.SetValue(10);
            store.Declare<string>("text", null);
            var state = store.GetStoreState();
            var snapshot = state.Variables.Single(v => v.Definition.Id == "v");
            Assert.That(state.StoreId, Is.EqualTo("store"));
            Assert.That(state.ParentId, Is.EqualTo("root"));
            Assert.That(state.IsRoot, Is.False);
            Assert.That(state.Variables.Select(v => v.Definition.Id), Is.EquivalentTo(new[] { "v", "text" }));
            Assert.That(snapshot.StoreId, Is.EqualTo("store"));
            Assert.That(snapshot.Definition, Is.SameAs(variable.Definition));
            Assert.That((object)snapshot, Is.Not.SameAs(variable.Data));
            Assert.That((object)snapshot, Is.Not.SameAs(variable));
            variable.ForceSetValue(20);
            variable.SetCurrentValue(30);
            store.Declare("later", true);
            Assert.That(snapshot.CurrentValue, Is.EqualTo(10));
            Assert.That(snapshot.OldValue, Is.EqualTo(0));
            Assert.That(state.Variables, Has.Count.EqualTo(2));
            Assert.That(state.Variables.Single(v => v.Definition.Id == "text").CurrentValue, Is.Null);
            Assert.Throws<NotSupportedException>(() => ((IList<EcaVariableSnapshot>)state.Variables).Clear());
            Assert.That(root.GetStoreState().IsRoot, Is.True);
        }

        [Test]
        public void SubtreeStateCopiesTopologyAndValuesWithoutAncestorsOrSiblings()
        {
            var system = new EcaVariablesSystem();
            system.CreateStore("A");
            var b = system.CreateStore("B", "A");
            system.CreateStore("C", "A");
            var d = system.CreateStore("D", "B");
            var e = system.CreateStore("E", "B");
            system.CreateStore("X");
            var variable = d.Declare("v", 1);
            var state = b.GetSubtreeState();
            Assert.That(state.RootStoreId, Is.EqualTo("B"));
            Assert.That(state.Stores.Select(s => s.StoreId), Is.EquivalentTo(new[] { "B", "D", "E" }));
            Assert.That(state.Stores.Single(s => s.StoreId == "B").ParentId, Is.EqualTo("A"));
            Assert.That(state.Stores.Single(s => s.StoreId == "D").ParentId, Is.EqualTo("B"));
            Assert.That(state.Stores.Single(s => s.StoreId == "E").ParentId, Is.EqualTo("B"));
            variable.SetValue(2);
            e.Declare("later", true);
            system.CreateStore("F", "D");
            Assert.That(state.Stores, Has.Count.EqualTo(3));
            Assert.That(state.Stores.Single(s => s.StoreId == "D").Variables.Single().CurrentValue, Is.EqualTo(1));
            Assert.That(state.Stores.Single(s => s.StoreId == "E").Variables, Is.Empty);
            Assert.Throws<NotSupportedException>(() => ((IList<EcaVariableStoreState>)state.Stores).Clear());
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void DirectAndProxyTrackedWritesUseSameOrderedBubbling(bool direct, bool force)
        {
            var system = new EcaVariablesSystem();
            var trace = new List<string>();
            var seen = new List<EcaVariableChanged>();
            // Aggregate subscription precedes creation of every Store.
            system.VariableChanged += change => { trace.Add("system"); seen.Add(change); };
            var root = system.CreateStore("root");
            var parent = system.CreateStore("parent", "root");
            var child = system.CreateStore("child", "parent");
            var sibling = system.CreateStore("sibling", "parent");
            var other = system.CreateStore("other");
            foreach (var store in new[] { child, parent, root, sibling, other })
                store.VariableChanged += change => { trace.Add(store.Id); seen.Add(change); };
            var variable = child.Declare("v", 0);
            Assert.That(trace, Is.Empty);
            if (direct)
            {
                if (force) variable.ForceSetValue(0); else variable.SetValue(1);
            }
            else
            {
                if (force) child.ForceSetValue("v", 0); else child.SetValue("v", 1);
            }
            Assert.That(trace, Is.EqualTo(new[] { "child", "parent", "root", "system" }));
            foreach (var change in seen)
            {
                Assert.That(change, Is.SameAs(seen[0]));
                Assert.That(change.StoreId, Is.EqualTo("child"));
                Assert.That(change.OldValue, Is.EqualTo(0));
                Assert.That(change.CurrentValue, Is.EqualTo(force ? 0 : 1));
            }
            trace.Clear();
            variable.SetValue(variable.GetValue<int>());
            child.SetValue("v", variable.GetValue<int>());
            variable.SetCurrentValue(5);
            child.SetCurrentValue("v", 6);
            Assert.That(trace, Is.Empty);
        }

        [Test]
        public void RootChangeReachesSystemOnceAndDoesNotReachOtherRootsOrChildren()
        {
            var system = new EcaVariablesSystem();
            var root = system.CreateStore("root");
            var child = system.CreateStore("child", "root");
            var other = system.CreateStore("other");
            var trace = new List<string>();
            root.VariableChanged += _ => trace.Add("root");
            child.VariableChanged += _ => trace.Add("child");
            other.VariableChanged += _ => trace.Add("other");
            system.VariableChanged += _ => trace.Add("system");
            root.Declare("v", 0).SetValue(1);
            Assert.That(trace, Is.EqualTo(new[] { "root", "system" }));
        }

        [Test]
        public void ReentrantParentMutationCompletesNestedRouteBeforeOuterContinues()
        {
            var system = new EcaVariablesSystem();
            var root = system.CreateStore("root");
            var parent = system.CreateStore("parent", "root");
            var child = system.CreateStore("child", "parent");
            var variable = child.Declare("v", 0);
            var trace = new List<string>();
            var completed = new List<EcaVariableChanged>();
            child.VariableChanged += c => trace.Add("child:" + c.CurrentValue);
            parent.VariableChanged += c =>
            {
                trace.Add("parent:" + c.CurrentValue);
                if ((int)c.CurrentValue == 1) variable.SetValue(2);
            };
            root.VariableChanged += c => trace.Add("root:" + c.CurrentValue);
            system.VariableChanged += c => { trace.Add("system:" + c.CurrentValue); completed.Add(c); };
            child.SetValue("v", 1);
            Assert.That(trace, Is.EqualTo(new[] { "child:1", "parent:1", "child:2", "parent:2", "root:2", "system:2", "root:1", "system:1" }));
            Assert.That(variable.CurrentValue, Is.EqualTo(2));
            Assert.That(completed[0].OldValue, Is.EqualTo(1));
            Assert.That(completed[0].CurrentValue, Is.EqualTo(2));
            Assert.That(completed[1].OldValue, Is.EqualTo(0));
            Assert.That(completed[1].CurrentValue, Is.EqualTo(1));
            Assert.That(completed[1], Is.Not.SameAs(completed[0]));
        }

        [TestCase("child")]
        [TestCase("parent")]
        [TestCase("root")]
        [TestCase("system")]
        public void SubscriberExceptionStopsLaterSubscribersAndRouteWithoutRollback(string failureAt)
        {
            var system = new EcaVariablesSystem();
            var root = system.CreateStore("root");
            var parent = system.CreateStore("parent", "root");
            var child = system.CreateStore("child", "parent");
            var variable = child.Declare("v", 0);
            var trace = new List<string>();
            var error = new InvalidOperationException("subscriber");
            foreach (var store in new[] { child, parent, root })
            {
                store.VariableChanged += _ => { trace.Add(store.Id); if (failureAt == store.Id) throw error; };
                store.VariableChanged += _ => trace.Add(store.Id + ":second");
            }
            system.VariableChanged += _ => { trace.Add("system"); if (failureAt == "system") throw error; };
            system.VariableChanged += _ => trace.Add("system:second");
            Assert.That(Assert.Throws<InvalidOperationException>(() => variable.SetValue(1)), Is.SameAs(error));
            var expected = new List<string>();
            foreach (var step in new[] { "child", "parent", "root", "system" })
            {
                expected.Add(step);
                if (step == failureAt) break;
                expected.Add(step + ":second");
            }
            Assert.That(trace, Is.EqualTo(expected));
            Assert.That(variable.CurrentValue, Is.EqualTo(1));
            Assert.That(variable.OldValue, Is.EqualTo(0));
            Assert.That(variable.Definition.DefaultValue, Is.EqualTo(0));
        }
    }
}
