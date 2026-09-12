using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EcaSystems.Core1;
using NUnit.Framework;
using static EcaSystems.Tests.Core1.LayerRules;

namespace EcaSystems.Tests.Core1
{
    [TestFixture]
    public sealed class EcaExecutionEngineTests
    {
        [Test]
        public void VerticalState_IsSharedAcrossRoles_AndGroupStateAcrossFires()
        {
            var events = new EcaEventRegistry();
            var evt = Event<string>(events);
            using var engine = new EcaExecutionEngine(events);
            var checks = new List<EcaExecutionRuleState<string>>();
            var actions = new List<EcaExecutionRuleState<string>>();
            for (var i = 0; i < 2; i++)
                engine.Register(Execution("r" + i, evt, s => { actions.Add(s); return Task.CompletedTask; },
                    s => { checks.Add(s); return true; }), new EcaExecutionMode(EcaOverlap.Allow));
            engine.Fire(evt, "first");
            engine.Fire(evt, "second");
            Assert.That(actions.Count, Is.EqualTo(4));
            for (var i = 0; i < 4; i++)
            {
                Assert.That(actions[i], Is.SameAs(checks[i]));
                EcaRuleState<string> baseState = actions[i];
                IEcaExecutionRuleState<object> covariant = actions[i];
                Assert.That(baseState.EventState, Is.EqualTo(i < 2 ? "first" : "second"));
                Assert.That(covariant.RuleExecutionGroupState, Is.SameAs(actions[i].RuleExecutionGroupState));
            }
            Assert.That(actions[0], Is.Not.SameAs(actions[2]));
            Assert.That(actions[0].RuleExecutionGroupState, Is.SameAs(actions[2].RuleExecutionGroupState));
            Assert.That(actions[0].RuleExecutionGroupState, Is.Not.SameAs(actions[1].RuleExecutionGroupState));
            Assert.That(actions[0].RuleExecutionGroupState.TotalStarted, Is.EqualTo(2));
            Assert.That(actions[0].RuleExecutionGroupState.TotalFinished, Is.EqualTo(2));
        }

        [Test]
        public void AllChecksPrecedeAdmissionAndActions_AndFalseDoesNotConsumeLimit()
        {
            var events = new EcaEventRegistry();
            var evt = Event<int>(events);
            using var engine = new EcaExecutionEngine(events);
            var trace = new List<string>();
            engine.Register(Execution("1", evt, _ => { trace.Add("Action1"); return Task.CompletedTask; },
                _ => { trace.Add("Check1"); return true; }), new EcaExecutionMode(EcaOverlap.Allow));
            engine.Register(Execution("2", evt, _ => { trace.Add("Action2"); return Task.CompletedTask; }, _ =>
            {
                trace.Add("Check2");
                Assert.That(Group(engine, "1").State.TotalStarted, Is.Zero);
                Assert.That(Group(engine, "2").State.TotalStarted, Is.Zero);
                return true;
            }), new EcaExecutionMode(EcaOverlap.Allow));
            engine.Register(Execution("false", evt, _ => throw new Exception("must not run"), _ => false),
                new EcaExecutionMode(EcaOverlap.Allow, 1));
            engine.Fire(evt, 0);
            CollectionAssert.AreEqual(new[] { "Check1", "Check2", "Action1", "Action2" }, trace);
            Assert.That(Group(engine, "false").State.TotalStarted, Is.Zero);
            Assert.That(Group(engine, "false").State.TotalFinished, Is.Zero);
        }

        [TestCase(-1, 4)]
        [TestCase(0, 0)]
        [TestCase(1, 1)]
        [TestCase(2, 2)]
        public void Limit_CountsOnlyAcceptedStarts_AfterEveryCondition(int limit, int expected)
        {
            var events = new EcaEventRegistry();
            var evt = Event<int>(events);
            using var engine = new EcaExecutionEngine(events);
            var checks = 0;
            var actions = 0;
            engine.Register(Execution("r", evt, _ => { actions++; return Task.CompletedTask; },
                _ => { checks++; return true; }), new EcaExecutionMode(EcaOverlap.Allow, limit));
            for (var i = 0; i < 4; i++) engine.Fire(evt, i);
            var group = Group(engine, "r");
            Assert.That(checks, Is.EqualTo(4));
            Assert.That(actions, Is.EqualTo(expected));
            Assert.That(group.State.TotalStarted, Is.EqualTo(expected));
            Assert.That(group.State.TotalFinished, Is.EqualTo(expected));
            Assert.That(group.Executions, Is.Empty);
        }

        [TestCase(EcaOverlap.Ignore, 1)]
        [TestCase(EcaOverlap.Allow, 2)]
        public async Task Overlap_ControlsActiveExecutions_WithoutSkippingConditions(EcaOverlap overlap, int active)
        {
            var events = new EcaEventRegistry();
            var evt = Event<int>(events);
            using var engine = new EcaExecutionEngine(events);
            using var gate = new ActionGate<EcaExecutionRuleState<int>>();
            var checks = 0;
            engine.Register(Execution("r", evt, gate.Run, _ => { checks++; return true; }), new EcaExecutionMode(overlap, 2));
            var group = Group(engine, "r");
            engine.Fire(evt, 0);
            engine.Fire(evt, 1);
            engine.Fire(evt, 2);
            Assert.That(checks, Is.EqualTo(3));
            Assert.That(group.Executions.Count, Is.EqualTo(active));
            Assert.That(group.State.TotalStarted, Is.EqualTo(active));
            gate.Dispose();
            await WaitUntil(() => group.Executions.Count == 0);
            engine.Fire(evt, 3);
            Assert.That(group.State.TotalStarted, Is.EqualTo(2));
            gate.Dispose();
            await WaitUntil(() => group.State.TotalFinished == 2);
            engine.Fire(evt, 4);
            Assert.That(group.State.TotalStarted, Is.EqualTo(2));
        }

        [TestCase("sync")]
        [TestCase("faulted")]
        [TestCase("null")]
        public void ActionFailure_DoesNotStopOtherPassedRules(string kind)
        {
            var events = new EcaEventRegistry();
            var evt = Event<int>(events);
            using var engine = new EcaExecutionEngine(events);
            var error = new InvalidOperationException("failure");
            EcaRuleExecution failed = null;
            var next = 0;
            engine.Register(Execution("failed", evt, _ =>
            {
                failed = Group(engine, "failed").Executions[0];
                if (kind == "sync") throw error;
                return kind == "null" ? null : Task.FromException(error);
            }), new EcaExecutionMode(EcaOverlap.Allow, 1));
            engine.Register(Execution("next", evt, _ => { next++; return Task.CompletedTask; }), new EcaExecutionMode(EcaOverlap.Allow));
            Assert.DoesNotThrow(() => engine.Fire(evt, 0));
            Assert.That(next, Is.EqualTo(1));
            Assert.That(failed.Status, Is.EqualTo(EcaRuleExecutionStatus.Failed));
            Assert.That(failed.Exception, Is.Not.Null);
            Assert.That(Group(engine, "failed").State.TotalFinished, Is.EqualTo(1));
            engine.Fire(evt, 1);
            Assert.That(next, Is.EqualTo(2));
            Assert.That(Group(engine, "failed").State.TotalStarted, Is.EqualTo(1));
        }

        [Test]
        public void ConditionFailure_PreventsAllAdmission()
        {
            var events = new EcaEventRegistry();
            var evt = Event<int>(events);
            using var engine = new EcaExecutionEngine(events);
            engine.Register(Execution("1", evt, _ => Task.CompletedTask), new EcaExecutionMode(EcaOverlap.Allow));
            engine.Register(Execution("2", evt, _ => Task.CompletedTask, _ => throw new InvalidOperationException("check")),
                new EcaExecutionMode(EcaOverlap.Allow));
            Assert.Throws<InvalidOperationException>(() => engine.Fire(evt, 0));
            Assert.That(Group(engine, "1").State.TotalStarted, Is.Zero);
            Assert.That(Group(engine, "2").State.TotalStarted, Is.Zero);
        }

        [Test]
        public async Task UnregisterAndReregister_OldAsyncFailureDoesNotAffectNewGroup()
        {
            var events = new EcaEventRegistry();
            var evt = Event<int>(events);
            using var engine = new EcaExecutionEngine(events);
            using var gate = new ActionGate<EcaExecutionRuleState<int>>();
            var rule = Execution("r", evt, gate.Run);
            var mode = new EcaExecutionMode(EcaOverlap.Ignore, 1);
            engine.Register(rule, mode);
            engine.Fire(evt, 1);
            var old = Group(engine, "r");
            var oldExecution = old.Executions[0];
            Assert.That(engine.Unregister(rule), Is.True);
            Assert.That(engine.TryGetGroup("r", out _), Is.False);
            engine.Fire(evt, 2);
            Assert.That(gate.States.Count, Is.EqualTo(1));
            engine.Register(rule, mode);
            engine.Fire(evt, 3);
            var current = Group(engine, "r");
            Assert.That(current.State, Is.Not.SameAs(old.State));
            Assert.That(current.Executions.Count, Is.EqualTo(1));
            var error = new InvalidOperationException("old failure");
            gate.Fail(0, error);
            await WaitUntil(() => old.State.TotalFinished == 1);
            Assert.That(oldExecution.Status, Is.EqualTo(EcaRuleExecutionStatus.Failed));
            Assert.That(oldExecution.Exception, Is.SameAs(error));
            Assert.That(current.State.TotalFinished, Is.Zero);
            gate.Complete(1);
            await WaitUntil(() => current.State.TotalFinished == 1);
        }

        [Test]
        public void RegistrationFailure_RollsBackAndPreservesExistingRegistration()
        {
            var events = new EcaEventRegistry();
            var evt = Event<int>(events);
            using var engine = new EcaExecutionEngine(events);
            var calls = 0;
            var rule = Execution("r", evt, _ => { calls++; return Task.CompletedTask; });
            Assert.Throws<ArgumentNullException>(() => engine.Register(rule, null));
            Assert.That(engine.TryGetGroup(rule.Id, out _), Is.False);
            engine.Fire(evt, 0);
            Assert.That(calls, Is.Zero);
            var mode = new EcaExecutionMode(EcaOverlap.Allow);
            engine.Register(rule, mode);
            var group = Group(engine, "r");
            Assert.Throws<InvalidOperationException>(() => engine.Register(rule, mode));
            var impostor = Execution("r", evt, _ => Task.CompletedTask);
            Assert.That(engine.Unregister(impostor), Is.False);
            Assert.That(Group(engine, "r"), Is.SameAs(group));
            engine.Fire(evt, 0);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void MultipleTypes_AndNestedFire_UseOneImmediatePipeline()
        {
            var events = new EcaEventRegistry();
            var a = Event<int>(events, "a");
            var b = Event<string>(events, "b");
            var empty = Event<EcaEventStateEmpty>(events, "empty");
            using var engine = new EcaExecutionEngine(events);
            var trace = new List<string>();
            engine.Register(Execution("a", a, _ =>
            {
                trace.Add("A starts");
                engine.Fire(b, "value");
                trace.Add("A continues");
                return Task.CompletedTask;
            }, _ => { trace.Add("Check A"); return true; }), new EcaExecutionMode(EcaOverlap.Allow));
            engine.Register(Execution("b", b, s => { trace.Add("B:" + s.EventState); return Task.CompletedTask; },
                _ => { trace.Add("Check B"); return true; }), new EcaExecutionMode(EcaOverlap.Allow));
            engine.Register(Execution("empty", empty, _ => { trace.Add("Empty"); return Task.CompletedTask; }), new EcaExecutionMode(EcaOverlap.Allow));
            engine.Fire(a, 1);
            engine.Fire(empty);
            CollectionAssert.AreEqual(new[] { "Check A", "A starts", "Check B", "B:value", "A continues", "Empty" }, trace);
        }

        [TestCase(EcaOverlap.Ignore, -1, 1)]
        [TestCase(EcaOverlap.Allow, 1, 1)]
        [TestCase(EcaOverlap.Allow, -1, 2)]
        public void RecursiveFire_SeesRunningAndStartedBeforeAction(EcaOverlap overlap, int limit, int actions)
        {
            var events = new EcaEventRegistry();
            var evt = Event<int>(events);
            using var engine = new EcaExecutionEngine(events);
            var calls = 0;
            var checks = 0;
            engine.Register(Execution("r", evt, s =>
            {
                calls++;
                if (s.EventState == 0) engine.Fire(evt, 1);
                return Task.CompletedTask;
            }, _ => { checks++; return true; }), new EcaExecutionMode(overlap, limit));
            engine.Fire(evt, 0);
            Assert.That(calls, Is.EqualTo(actions));
            Assert.That(checks, Is.EqualTo(2));
            Assert.That(Group(engine, "r").State.TotalStarted, Is.EqualTo(actions));
            Assert.That(Group(engine, "r").State.TotalFinished, Is.EqualTo(actions));
        }

        [Test]
        public async Task Dispose_ClosesFacadeButActiveActionFinishes()
        {
            var events = new EcaEventRegistry();
            var evt = Event<int>(events);
            using var engine = new EcaExecutionEngine(events);
            using var gate = new ActionGate<EcaExecutionRuleState<int>>();
            var rule = Execution("r", evt, gate.Run);
            var mode = new EcaExecutionMode(EcaOverlap.Allow);
            engine.Register(rule, mode);
            engine.Fire(evt, 1);
            var group = Group(engine, "r");
            var execution = group.Executions[0];
            engine.Dispose();
            engine.Dispose();
            Assert.Throws<ObjectDisposedException>(() => engine.Fire(evt, 2));
            Assert.Throws<ObjectDisposedException>(() => engine.Register(rule, mode));
            Assert.Throws<ObjectDisposedException>(() => engine.Unregister(rule));
            Assert.That(execution.Status, Is.EqualTo(EcaRuleExecutionStatus.Running));
            Assert.That(events.IsRegistered(evt), Is.True);
            gate.Complete(0);
            await WaitUntil(() => group.State.TotalFinished == 1);
            Assert.That(execution.Status, Is.EqualTo(EcaRuleExecutionStatus.Completed));
        }
    }
}
