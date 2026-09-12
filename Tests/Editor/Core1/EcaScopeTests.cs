using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EcaSystems.Core1;
using NUnit.Framework;
using static EcaSystems.Tests.Core1.LayerRules;

namespace EcaSystems.Tests.Core1
{
    [TestFixture]
    public sealed class EcaScopeTests
    {
        [Test]
        public void VerticalState_HasPayloadGroupAndScope_AndIsSameAcrossRoles()
        {
            var events = new EcaEventRegistry();
            var evt = Event<string>(events);
            using var owner = new EcaScopeEngine(events);
            var scope = owner.CreateScope("scope");
            var checks = new List<EcaScopeRuleState<string>>();
            var actions = new List<EcaScopeRuleState<string>>();
            scope.Register(Scope("r", evt, s => { actions.Add(s); return Task.CompletedTask; },
                s => { checks.Add(s); return true; }), new EcaExecutionMode(EcaOverlap.Allow));
            scope.Fire(evt, "one");
            scope.Fire(evt, "two");
            Assert.That(actions.Count, Is.EqualTo(2));
            for (var i = 0; i < 2; i++)
            {
                EcaExecutionRuleState<string> executionState = actions[i];
                EcaRuleState<string> baseState = executionState;
                IEcaScopeRuleState<object> covariant = actions[i];
                Assert.That(actions[i], Is.SameAs(checks[i]));
                Assert.That(baseState.EventState, Is.EqualTo(i == 0 ? "one" : "two"));
                Assert.That(covariant.ScopeState, Is.SameAs(scope.State));
                Assert.That(executionState.RuleExecutionGroupState, Is.SameAs(checks[0].RuleExecutionGroupState));
            }
            Assert.That(actions[0], Is.Not.SameAs(actions[1]));
            Assert.That(actions[0].RuleExecutionGroupState.TotalFinished, Is.EqualTo(2));
        }

        [TestCase(EcaOverlap.Ignore, 1)]
        [TestCase(EcaOverlap.Allow, 2)]
        public async Task SharedRule_HasIsolatedLimitGroupsAndActiveExecutions(EcaOverlap overlap, int starts)
        {
            var events = new EcaEventRegistry();
            var evt = Event<int>(events);
            using var owner = new EcaScopeEngine(events);
            var a = owner.CreateScope("a");
            var b = owner.CreateScope("b");
            using var gate = new ActionGate<EcaScopeRuleState<int>>();
            var checkedStates = new List<EcaScopeRuleState<int>>();
            var rule = Scope("shared", evt, gate.Run, s => { checkedStates.Add(s); return true; });
            var mode = new EcaExecutionMode(overlap, 2);
            a.Register(rule, mode);
            b.Register(rule, mode);
            a.Fire(evt, 1);
            a.Fire(evt, 2);
            Assert.That(gate.States.Count, Is.EqualTo(starts));
            b.Fire(evt, 3);
            b.Fire(evt, 4);
            Assert.That(gate.States.Count, Is.EqualTo(starts * 2));
            var aState = checkedStates[0].RuleExecutionGroupState;
            var bState = checkedStates[2].RuleExecutionGroupState;
            Assert.That(aState, Is.Not.SameAs(bState));
            Assert.That(aState.TotalStarted, Is.EqualTo(starts));
            Assert.That(bState.TotalStarted, Is.EqualTo(starts));
            gate.Dispose();
            await WaitUntil(() => aState.TotalFinished == starts && bState.TotalFinished == starts);
            a.Fire(evt, 5);
            a.Fire(evt, 6);
            Assert.That(aState.TotalStarted, Is.EqualTo(2));
            Assert.That(bState.TotalStarted, Is.EqualTo(starts));
            gate.Dispose();
            await WaitUntil(() => aState.TotalFinished == 2);
            a.Fire(evt, 7);
            Assert.That(aState.TotalStarted, Is.EqualTo(2));
        }

        [Test]
        public void SharedEventRegistry_IsLiveAndExplicit_ForAllLocalScopes()
        {
            var events = new EcaEventRegistry();
            using var owner = new EcaScopeEngine(events);
            var a = owner.CreateScope("a");
            var b = owner.CreateScope("b");
            var evt = new EcaEvent<int>("later", "Later");
            Assert.Throws<InvalidOperationException>(() => a.Fire(evt, 1));
            Assert.That(events.IsRegistered(evt), Is.False);
            var calls = new List<string>();
            var rule = Scope("r", evt, s => { calls.Add(s.ScopeState.ScopeId); return Task.CompletedTask; });
            Assert.Throws<InvalidOperationException>(() => a.Register(rule, new EcaExecutionMode(EcaOverlap.Allow)));
            events.Register(evt);
            a.Register(rule, new EcaExecutionMode(EcaOverlap.Allow));
            b.Register(rule, new EcaExecutionMode(EcaOverlap.Allow));
            a.Fire(evt, 1);
            b.Fire(evt, 2);
            CollectionAssert.AreEqual(new[] { "a", "b" }, calls);
            a.Dispose();
            Assert.That(events.IsRegistered(evt), Is.True);
            b.Fire(evt, 3);
            CollectionAssert.AreEqual(new[] { "a", "b", "b" }, calls);
        }

        [Test]
        public void Hierarchy_FireIsLocal_AndDisposeOnlyRemovesOwnedDescendants()
        {
            var events = new EcaEventRegistry();
            var evt = Event<int>(events);
            using var owner = new EcaScopeEngine(events);
            var root = owner.CreateScope("scope-1");
            var child = root.CreateScope("child");
            var grandchild = child.CreateScope();
            var sibling = root.CreateScope("sibling");
            var other = owner.CreateScope("other");
            Assert.That(grandchild.ScopeId, Is.EqualTo("scope-2"));
            Assert.That(child.ParentScopeId, Is.EqualTo(root.ScopeId));
            Assert.That(grandchild.ParentScopeId, Is.EqualTo(child.ScopeId));
            Assert.That(root.ParentScopeId, Is.Null);
            var calls = new List<string>();
            var rule = Scope("r", evt, s => { calls.Add(s.ScopeState.ScopeId); return Task.CompletedTask; });
            foreach (var scope in new[] { root, child, grandchild, sibling, other })
                scope.Register(rule, new EcaExecutionMode(EcaOverlap.Allow));
            child.Fire(evt, 0);
            root.Fire(evt, 0);
            CollectionAssert.AreEqual(new[] { "child", "scope-1" }, calls);
            Assert.Throws<InvalidOperationException>(() => owner.CreateScope("child"));
            Assert.Throws<InvalidOperationException>(() => child.CreateScope("other"));
            child.Dispose();
            Assert.That(child.IsDisposed && grandchild.IsDisposed, Is.True);
            Assert.That(sibling.IsDisposed || root.IsDisposed || other.IsDisposed, Is.False);
            Assert.That(owner.ScopeCount, Is.EqualTo(3));
            root.Dispose();
            Assert.That(sibling.IsDisposed, Is.True);
            Assert.That(other.IsDisposed, Is.False);
            Assert.That(owner.ScopeCount, Is.EqualTo(1));
            owner.Dispose();
            owner.Dispose();
            Assert.That(other.IsDisposed, Is.True);
            Assert.That(owner.ScopeCount, Is.Zero);
            Assert.Throws<ObjectDisposedException>(() => owner.CreateScope());
        }

        [Test]
        public void DisposedAndStaleScope_CannotOperateOrRemoveReusedIdentity()
        {
            var events = new EcaEventRegistry();
            var evt = Event<int>(events);
            using var owner = new EcaScopeEngine(events);
            var old = owner.CreateScope("same");
            var oldState = old.State;
            old.Dispose();
            var replacement = owner.CreateScope("same");
            old.Dispose();
            Assert.That(owner.TryGetScope("same", out var found), Is.True);
            Assert.That(found, Is.SameAs(replacement));
            Assert.That(replacement.State, Is.Not.SameAs(oldState));
            var rule = Scope("r", evt, _ => Task.CompletedTask);
            Assert.Throws<ObjectDisposedException>(() => old.Fire(evt, 0));
            Assert.Throws<ObjectDisposedException>(() => old.Register(rule, new EcaExecutionMode(EcaOverlap.Allow)));
            Assert.Throws<ObjectDisposedException>(() => old.Unregister(rule));
            Assert.Throws<ObjectDisposedException>(() => old.CreateScope("child"));
            Assert.That(replacement.CreateScope("child").ParentScopeId, Is.EqualTo("same"));
        }

        [TestCase("")]
        [TestCase(" \t")]
        public void ScopeIdentity_RejectsBlankNames(string id)
        {
            using var owner = new EcaScopeEngine(new EcaEventRegistry());
            Assert.Throws<ArgumentException>(() => owner.CreateScope(id));
            Assert.Throws<ArgumentException>(() => new EcaScopeState(id));
            Assert.Throws<ArgumentException>(() => new EcaScopeState(null));
            Assert.That(owner.ScopeCount, Is.Zero);
        }

        [Test]
        public async Task RunningAction_SurvivesScopeDispose_AndCannotAffectReplacementState()
        {
            var events = new EcaEventRegistry();
            var evt = Event<int>(events);
            using var owner = new EcaScopeEngine(events);
            using var gate = new ActionGate<EcaScopeRuleState<int>>();
            var rule = Scope("r", evt, gate.Run);
            var mode = new EcaExecutionMode(EcaOverlap.Ignore, 1);
            var scope = owner.CreateScope("same");
            scope.Register(rule, mode);
            scope.Fire(evt, 1);
            var oldState = gate.States[0];
            scope.Dispose();
            Assert.That(oldState.RuleExecutionGroupState.TotalFinished, Is.Zero);
            var replacement = owner.CreateScope("same");
            replacement.Register(rule, mode);
            replacement.Fire(evt, 2);
            var newState = gate.States[1];
            Assert.That(newState.ScopeState, Is.Not.SameAs(oldState.ScopeState));
            Assert.That(newState.RuleExecutionGroupState, Is.Not.SameAs(oldState.RuleExecutionGroupState));
            gate.Complete(0);
            await WaitUntil(() => oldState.RuleExecutionGroupState.TotalFinished == 1);
            Assert.That(newState.RuleExecutionGroupState.TotalFinished, Is.Zero);
            gate.Complete(1);
            await WaitUntil(() => newState.RuleExecutionGroupState.TotalFinished == 1);
        }

        [Test]
        public async Task ScopeUnregister_ReregistersWithFreshGroup_WhileOldActionIsRunning()
        {
            var events = new EcaEventRegistry();
            var evt = Event<int>(events);
            using var owner = new EcaScopeEngine(events);
            var scope = owner.CreateScope();
            using var gate = new ActionGate<EcaScopeRuleState<int>>();
            var rule = Scope("r", evt, gate.Run);
            var mode = new EcaExecutionMode(EcaOverlap.Ignore, 1);
            scope.Register(rule, mode);
            scope.Fire(evt, 0);
            Assert.That(scope.Unregister(rule), Is.True);
            Assert.That(scope.Unregister(rule), Is.False);
            scope.Fire(evt, 1);
            Assert.That(gate.States.Count, Is.EqualTo(1));
            scope.Register(rule, mode);
            scope.Fire(evt, 2);
            Assert.That(gate.States.Count, Is.EqualTo(2));
            var old = gate.States[0].RuleExecutionGroupState;
            var current = gate.States[1].RuleExecutionGroupState;
            Assert.That(old, Is.Not.SameAs(current));
            gate.Fail(0, new InvalidOperationException("old scope action"));
            await WaitUntil(() => old.TotalFinished == 1);
            Assert.That(current.TotalFinished, Is.Zero);
            gate.Complete(1);
            await WaitUntil(() => current.TotalFinished == 1);
        }

        [Test]
        public async Task NestedFire_IsLocalImmediateAndDoesNotAwaitInnerAction()
        {
            var events = new EcaEventRegistry();
            var a = Event<int>(events, "a");
            var b = Event<string>(events, "b");
            var empty = Event<EcaEventStateEmpty>(events, "empty");
            using var owner = new EcaScopeEngine(events);
            var scope = owner.CreateScope("local");
            var sibling = owner.CreateScope("sibling");
            using var gate = new ActionGate<EcaScopeRuleState<string>>();
            var trace = new List<string>();
            var mode = new EcaExecutionMode(EcaOverlap.Allow);
            scope.Register(Scope("a1", a, _ =>
            {
                trace.Add("A1 starts");
                scope.Fire(b, "nested");
                trace.Add("A1 continues");
                return Task.CompletedTask;
            }, _ => { trace.Add("Check A1"); return true; }), mode);
            scope.Register(Scope("a2", a, _ => { trace.Add("A2"); return Task.CompletedTask; },
                _ => { trace.Add("Check A2"); return true; }), mode);
            scope.Register(Scope("b1", b, async s =>
            {
                trace.Add("B1 starts:" + s.EventState);
                await gate.Run(s);
                trace.Add("B1 completes");
            }, _ => { trace.Add("Check B1"); return true; }), mode);
            scope.Register(Scope("b2", b, _ => { trace.Add("B2"); return Task.CompletedTask; },
                _ => { trace.Add("Check B2"); return true; }), mode);
            sibling.Register(Scope("b1", b, _ => { trace.Add("sibling"); return Task.CompletedTask; }), mode);
            scope.Register(Scope("empty", empty, _ => { trace.Add("Empty"); return Task.CompletedTask; }), mode);
            scope.Fire(a, 1);
            CollectionAssert.AreEqual(new[] { "Check A1", "Check A2", "A1 starts", "Check B1", "Check B2",
                "B1 starts:nested", "B2", "A1 continues", "A2" }, trace);
            Assert.That(gate.States[0].RuleExecutionGroupState.TotalFinished, Is.Zero);
            gate.Complete(0);
            await WaitUntil(() => gate.States[0].RuleExecutionGroupState.TotalFinished == 1);
            Assert.That(trace[trace.Count - 1], Is.EqualTo("B1 completes"));
            scope.Fire(empty);
            Assert.That(trace[trace.Count - 1], Is.EqualTo("Empty"));
        }

        [Test]
        public void ScopeFailure_IsObservedByInheritedExecution_AndOtherRulesContinue()
        {
            var events = new EcaEventRegistry();
            var evt = Event<int>(events);
            using var owner = new EcaScopeEngine(events);
            var scope = owner.CreateScope();
            EcaRuleExecutionGroupState failed = null;
            var next = 0;
            scope.Register(Scope("bad", evt, s => { failed = s.RuleExecutionGroupState; throw new InvalidOperationException("failure"); }),
                new EcaExecutionMode(EcaOverlap.Allow, 1));
            scope.Register(Scope("next", evt, _ => { next++; return Task.CompletedTask; }), new EcaExecutionMode(EcaOverlap.Allow));
            Assert.DoesNotThrow(() => scope.Fire(evt, 0));
            Assert.That(next, Is.EqualTo(1));
            Assert.That(failed.TotalStarted, Is.EqualTo(1));
            Assert.That(failed.TotalFinished, Is.EqualTo(1));
            scope.Fire(evt, 1);
            Assert.That(next, Is.EqualTo(2));
            Assert.That(failed.TotalStarted, Is.EqualTo(1));
        }
    }
}
