using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EcaSystems.Core2;
using NUnit.Framework;
using static EcaSystems.Tests.Core2.ExecutionTestSupport;

namespace EcaSystems.Tests.Core2
{
    [TestFixture]
    public sealed class EcaScopeRuntimeTests : ExecutionTestFixture
    {
        private sealed class C : IEcaScopeConditionContext { }
        private sealed class A : IEcaScopeActionContext { }
        private EcaScopeRuntime _owner;

        [SetUp]
        public void SetUpOwner() => _owner = new EcaScopeRuntime(Events, new EcaBaseConditionChecker(), new EcaBaseActionRunner());

        [TearDown]
        public void DisposeOwner() => _owner.Dispose();

        private Rule<int, EcaScopeRuleState<int>> Rule(Func<EcaScopeRuleState<int>, Task> run)
        {
            return new Rule<int, EcaScopeRuleState<int>>
            {
                Id = "shared", Event = Event,
                Action = new ExecutionTestSupport.Action<EcaScopeRuleState<int>, A> { Handler = (state, context) => run(state) }
            };
        }

        private void Fire(EcaScope scope, int value = 1) => scope.Fire<int>(Event, value, new C(), new A());

        private void AssertClosed(EcaScope scope)
        {
            var rule = Rule(s => Task.CompletedTask);
            var mode = new EcaExecutionMode(EcaExecutionModeOverlap.Allow);
            Assert.That(scope.IsDisposed, Is.True);
            Assert.Throws<ObjectDisposedException>(() => scope.CreateScope());
            Assert.Throws<ObjectDisposedException>(() => scope.Register(rule, mode));
            Assert.Throws<ObjectDisposedException>(() => scope.Register(rule, mode,
                (Func<IEcaExecutionRuleState<int>, EcaScopeState, EcaScopeRuleState<int>>)null));
            Assert.Throws<ObjectDisposedException>(() => scope.Unregister(rule));
            Assert.Throws<ObjectDisposedException>(() => Fire(scope));
            Assert.Throws<ObjectDisposedException>(() => scope.Fire<int, EcaScopeRuleState<int>>(
                Event, 0, (r, e) => new EcaScopeRuleState<int>(e, new EcaExecutionGroupState(), scope.State), new C(), new A()));
            Assert.Throws<ObjectDisposedException>(() => scope.GetGroup("shared"));
            Assert.Throws<ObjectDisposedException>(() => scope.TryGetGroup("shared", out _));
        }

        [Test]
        public void CreateScope_TracksHierarchyAndGeneratesIdsSkippingActiveNames()
        {
            Assert.That(_owner.ScopeCount, Is.Zero);
            var root = _owner.CreateScope("scope-1");
            var child = root.CreateScope("child");
            var grandchild = child.CreateScope();
            var sibling = root.CreateScope(null);
            Assert.That(root.ParentScopeId, Is.Null);
            Assert.That(child.ParentScopeId, Is.EqualTo(root.ScopeId));
            Assert.That(grandchild.ParentScopeId, Is.EqualTo(child.ScopeId));
            Assert.That(grandchild.ScopeId, Is.EqualTo("scope-2"));
            Assert.That(sibling.ScopeId, Is.EqualTo("scope-3"));
            Assert.That(_owner.ScopeCount, Is.EqualTo(4));
            Assert.That(_owner.TryGetScope("child", out var found), Is.True);
            Assert.That(found, Is.SameAs(child));
            Assert.That(_owner.TryGetScope(null, out _), Is.False);
            Assert.That(_owner.TryGetScope("missing", out _), Is.False);
            Assert.Throws<InvalidOperationException>(() => _owner.CreateScope("child"));
            Assert.Throws<InvalidOperationException>(() => child.CreateScope("scope-1"));
            Assert.That(_owner.ScopeCount, Is.EqualTo(4));
        }

        [TestCase("")]
        [TestCase(" \t")]
        public void CreateScope_RejectsBlankIdsWithoutChangingRegistry(string id)
        {
            var root = _owner.CreateScope();
            Assert.Throws<ArgumentException>(() => _owner.CreateScope(id));
            Assert.Throws<ArgumentException>(() => root.CreateScope(id));
            Assert.That(_owner.ScopeCount, Is.EqualTo(1));
        }

        [Test]
        public void Dispose_RecursivelyClosesDescendantsButLeavesParentSiblingsAndOtherRoots()
        {
            var root = _owner.CreateScope("root");
            var child = root.CreateScope("child");
            var grandchild = child.CreateScope("grandchild");
            var sibling = root.CreateScope("sibling");
            var other = _owner.CreateScope("other");
            child.Dispose();
            child.Dispose();
            AssertClosed(child);
            AssertClosed(grandchild);
            Assert.That(root.IsDisposed || sibling.IsDisposed || other.IsDisposed, Is.False);
            Assert.That(_owner.ScopeCount, Is.EqualTo(3));
            Assert.That(_owner.TryGetScope("child", out _), Is.False);
            Assert.That(_owner.TryGetScope("grandchild", out _), Is.False);
            root.Dispose();
            AssertClosed(root);
            AssertClosed(sibling);
            Assert.That(other.IsDisposed, Is.False);
            Assert.That(_owner.ScopeCount, Is.EqualTo(1));
        }

        [Test]
        public void Dispose_ManagerClosesAllScopesAndPreservesReadOnlyLookupSemantics()
        {
            var root = _owner.CreateScope("root");
            var child = root.CreateScope();
            var other = _owner.CreateScope();
            _owner.Dispose();
            _owner.Dispose();
            AssertClosed(root);
            AssertClosed(child);
            AssertClosed(other);
            Assert.That(_owner.ScopeCount, Is.Zero);
            Assert.That(_owner.TryGetScope("root", out _), Is.False);
            Assert.That(_owner.TryGetScope(null, out _), Is.False);
            Assert.Throws<ObjectDisposedException>(() => _owner.CreateScope());
            Assert.That(root.ScopeId, Is.EqualTo("root"));
            Assert.That(child.ParentScopeId, Is.EqualTo("root"));
            Assert.That(root.State.ScopeId, Is.EqualTo("root"));
            Assert.That(Events.CheckRegistered(Event), Is.True);
        }

        [Test]
        public void ScopeIdReuse_StaleChildCannotDamageReplacementOrItsHierarchy()
        {
            var root = _owner.CreateScope("root");
            var old = root.CreateScope("scene");
            old.CreateScope("nested");
            old.Dispose();
            var replacement = root.CreateScope("scene");
            var nested = replacement.CreateScope("nested");
            old.Dispose();
            AssertClosed(old);
            Assert.That(_owner.TryGetScope("scene", out var found), Is.True);
            Assert.That(found, Is.SameAs(replacement));
            Assert.That(replacement.State, Is.Not.SameAs(old.State));
            Assert.That(replacement.IsDisposed || nested.IsDisposed, Is.False);
            root.Dispose();
            AssertClosed(replacement);
            AssertClosed(nested);
            Assert.That(_owner.ScopeCount, Is.Zero);
        }

        [Test]
        public void Fire_RemainsLocalToParentOrChild()
        {
            var parent = _owner.CreateScope("parent");
            var child = parent.CreateScope("child");
            var calls = new List<string>();
            var rule = Rule(s => { calls.Add(s.ScopeState.ScopeId); return Task.CompletedTask; });
            parent.Register(rule, new EcaExecutionMode(EcaExecutionModeOverlap.Allow));
            child.Register(rule, new EcaExecutionMode(EcaExecutionModeOverlap.Allow));
            Fire(parent);
            Assert.That(calls, Is.EqualTo(new[] { "parent" }));
            Fire(child);
            Assert.That(calls, Is.EqualTo(new[] { "parent", "child" }));
            Assert.That(parent.GetGroup("shared").State.TotalStarted, Is.EqualTo(1));
            Assert.That(child.GetGroup("shared").State.TotalStarted, Is.EqualTo(1));
        }

        [Test]
        public async Task Fire_SharedRule_KeepsScopeStateLimitsAndRegistriesIndependent()
        {
            var a = _owner.CreateScope("a");
            var b = _owner.CreateScope("b");
            var gateA = NewGate();
            var gateB = NewGate();
            var seen = new List<EcaScopeRuleState<int>>();
            var rule = Rule(s => { seen.Add(s); return s.ScopeState.ScopeId == "a" ? gateA.Task : gateB.Task; });
            var mode = new EcaExecutionMode(EcaExecutionModeOverlap.Ignore, 1);
            a.Register(rule, mode);
            b.Register(rule, mode);
            var groupA = a.GetGroup("shared");
            var groupB = b.GetGroup("shared");
            Fire(a);
            Fire(a);
            Assert.That(groupA.State.TotalStarted, Is.EqualTo(1));
            Assert.That(groupB.State.TotalStarted, Is.Zero);
            Fire(b);
            Assert.That(seen, Has.Count.EqualTo(2));
            Assert.That(seen[0].ScopeState, Is.SameAs(a.State));
            Assert.That(seen[1].ScopeState, Is.SameAs(b.State));
            Assert.That(groupA.State, Is.Not.SameAs(groupB.State));
            Assert.That(groupA.Executions, Has.Count.EqualTo(1));
            Assert.That(groupB.Executions, Has.Count.EqualTo(1));
            Assert.That(a.Unregister(rule), Is.True);
            Assert.That(a.TryGetGroup("shared", out _), Is.False);
            Assert.That(b.GetGroup("shared"), Is.SameAs(groupB));
            gateA.Complete();
            await WaitUntil(() => groupA.State.TotalFinished == 1);
            Assert.That(groupB.State.TotalFinished, Is.Zero);
            gateB.Complete();
            await WaitUntil(() => groupB.State.TotalFinished == 1);
            Fire(b);
            Assert.That(seen, Has.Count.EqualTo(2), "Completion does not restore B's lifetime limit.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task Dispose_RunningAction_CompletesWithoutAffectingReplacement(bool disposeParent)
        {
            var parent = _owner.CreateScope("parent");
            var old = parent.CreateScope("scene");
            var oldGate = NewGate();
            var newGate = NewGate();
            var seen = new List<EcaScopeRuleState<int>>();
            var rule = Rule(s => { seen.Add(s); return seen.Count == 1 ? oldGate.Task : newGate.Task; });
            var mode = new EcaExecutionMode(EcaExecutionModeOverlap.Ignore, 1);
            old.Register(rule, mode);
            Fire(old);
            var oldGroup = old.GetGroup("shared");
            var oldExecution = oldGroup.Executions[0];
            if (disposeParent) parent.Dispose();
            else old.Dispose();
            AssertClosed(old);
            Assert.That(oldExecution.Status, Is.EqualTo(EcaExecutionStatus.Running));
            var replacement = _owner.CreateScope("scene");
            replacement.Register(rule, mode);
            Fire(replacement);
            var newGroup = replacement.GetGroup("shared");
            Assert.That(seen[0].ScopeState, Is.SameAs(old.State));
            Assert.That(seen[1].ScopeState, Is.SameAs(replacement.State));
            Assert.That(old.State, Is.Not.SameAs(replacement.State));
            Assert.That(oldGroup, Is.Not.SameAs(newGroup));
            Assert.That(oldGroup.State, Is.Not.SameAs(newGroup.State));
            oldGate.Complete();
            await WaitUntil(() => oldGroup.State.TotalFinished == 1);
            Assert.That(oldExecution.Status, Is.EqualTo(EcaExecutionStatus.Completed));
            Assert.That(newGroup.State.TotalFinished, Is.Zero);
            Assert.That(newGroup.Executions, Has.Count.EqualTo(1));
            old.Dispose();
            Assert.That(_owner.TryGetScope("scene", out var found), Is.True);
            Assert.That(found, Is.SameAs(replacement));
            Assert.That(replacement.CreateScope().ParentScopeId, Is.EqualTo("scene"));
            newGate.Complete();
            await WaitUntil(() => newGroup.State.TotalFinished == 1);
        }

        [Test]
        public async Task Dispose_ManagerDoesNotCancelRunningActions()
        {
            var scope = _owner.CreateScope();
            var gate = NewGate();
            scope.Register(Rule(s => gate.Task), new EcaExecutionMode(EcaExecutionModeOverlap.Allow));
            Fire(scope);
            var group = scope.GetGroup("shared");
            var execution = group.Executions[0];
            _owner.Dispose();
            AssertClosed(scope);
            Assert.That(execution.Status, Is.EqualTo(EcaExecutionStatus.Running));
            gate.Complete();
            await WaitUntil(() => group.Executions.Count == 0);
            Assert.That(execution.Status, Is.EqualTo(EcaExecutionStatus.Completed));
            Assert.That(group.State.TotalFinished, Is.EqualTo(1));
        }

        [Test]
        public void Fire_UsesOnlyLocalRegistryAndCallerState()
        {
            var parent = _owner.CreateScope("parent");
            var child = parent.CreateScope("child");
            var calls = new List<string>();
            var rule = Rule(s => { calls.Add(s.ScopeState.ScopeId); return Task.CompletedTask; });
            parent.Register(rule, new EcaExecutionMode(EcaExecutionModeOverlap.Ignore, 0));
            child.Register(rule, new EcaExecutionMode(EcaExecutionModeOverlap.Allow));
            parent.Fire<int, EcaScopeRuleState<int>>(Event, 1,
                (r, e) => new EcaScopeRuleState<int>(e, new EcaExecutionGroupState(), parent.State), new C(), new A());
            Assert.That(calls, Is.EqualTo(new[] { "parent" }));
            Assert.That(parent.GetGroup("shared").State.TotalStarted, Is.Zero);
            Assert.That(child.GetGroup("shared").State.TotalStarted, Is.Zero);
        }

        [Test]
        public void SharedEventRegistry_StaysLiveForExistingScopes()
        {
            var a = _owner.CreateScope("a");
            var b = _owner.CreateScope("b");
            var extra = new BaseTestSupport.Event<int> { Id = "late" };
            var calls = 0;
            var rule = Rule(s => { calls++; return Task.CompletedTask; });
            rule.Event = extra;
            Assert.Throws<InvalidOperationException>(() => a.Register(rule, new EcaExecutionMode(EcaExecutionModeOverlap.Allow)));
            Events.Register(extra);
            a.Register(rule, new EcaExecutionMode(EcaExecutionModeOverlap.Allow));
            b.Register(rule, new EcaExecutionMode(EcaExecutionModeOverlap.Allow));
            a.Fire<int>(extra, 1, new C(), new A());
            Assert.That(calls, Is.EqualTo(1));
            b.Fire<int>(extra, 1, new C(), new A());
            Assert.That(calls, Is.EqualTo(2));
        }
    }
}
