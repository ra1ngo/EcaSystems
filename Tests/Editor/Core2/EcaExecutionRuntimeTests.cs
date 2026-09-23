using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EcaSystems.Core2;
using NUnit.Framework;
using static EcaSystems.Tests.Core2.ExecutionTestSupport;

namespace EcaSystems.Tests.Core2
{
    [TestFixture]
    public sealed class EcaExecutionRuntimeTests : ExecutionTestFixture
    {
        private static State CreateState(int value, EcaExecutionGroupState state) => new(value, state);
        private EcaExecutionMode Allow => new(EcaExecutionModeOverlap.Allow);

        [Test]
        public void Fire_ChecksAllConditionsBeforeExecutionsAndPreservesSelectionOrder()
        {
            var trace = new List<string>();
            var states = new List<State>();
            var contexts = new List<object>();
            foreach (var id in new[] { "A", "B", "C" })
            {
                RegisterRule(NewRule(id, (state, context) =>
                {
                    states.Add(state);
                    contexts.Add(context);
                    trace.Add("Condition " + id);
                    return id != "B";
                }, (state, context) =>
                {
                    states.Add(state);
                    contexts.Add(context);
                    trace.Add("Action " + id);
                    return Task.CompletedTask;
                }), Allow, CreateState);
            }
            var otherEvent = new BaseTestSupport.Event<int> { Id = "other" };
            Events.Register(otherEvent);
            var other = NewRule("other", (state, context) => { trace.Add("wrong condition"); return true; },
                (state, context) => { trace.Add("wrong action"); return Task.CompletedTask; });
            other.Event = otherEvent;
            RegisterRule(other, Allow, CreateState);

            Fire(42);
            Assert.That(trace, Is.EqualTo(new[] { "Condition A", "Condition B", "Condition C", "Action A", "Action C" }));
            Assert.That(contexts, Is.EqualTo(new object[] { Conditions, Conditions, Conditions, Actions, Actions }));
            Assert.That(states, Has.Count.EqualTo(5));
            foreach (var state in states)
            {
                Assert.That(state.EventState, Is.EqualTo(42));
                Assert.That(state.Extra, Is.EqualTo("extended state"));
            }
            Assert.That(states[0], Is.Not.SameAs(states[3]));
            Assert.That(states[0].ExecutionGroupState, Is.SameAs(states[3].ExecutionGroupState));
            Assert.That(Runtime.GetGroup("A").State.TotalFinished, Is.EqualTo(1));
            Assert.That(Runtime.GetGroup("B").State.TotalStarted, Is.Zero);
            Assert.That(Runtime.GetGroup("C").State.TotalFinished, Is.EqualTo(1));
            Assert.That(Runtime.GetGroup("other").State.TotalStarted, Is.Zero);
        }

        [Test]
        public void Fire_NullConditionStillWaitsForOtherChecks()
        {
            var trace = new List<string>();
            RegisterRule(NewRule("optional", run: (state, context) =>
            {
                trace.Add("Action optional");
                return Task.CompletedTask;
            }), Allow, CreateState);
            RegisterRule(NewRule("failed", (state, context) =>
            {
                trace.Add("Condition failed");
                return false;
            }), Allow, CreateState);
            Fire();
            Assert.That(trace, Is.EqualTo(new[] { "Condition failed", "Action optional" }));
            Assert.That(Runtime.GetGroup("failed").State.TotalStarted, Is.Zero);
        }

        [TestCase("condition")]
        [TestCase("state")]
        public void Fire_ConditionPhaseErrorAbortsBeforeAnyExecution(string source)
        {
            var error = new InvalidOperationException("condition phase");
            var calls = 0;
            RegisterRule(NewRule("first", run: (state, context) => { calls++; return Task.CompletedTask; }), Allow, CreateState);
            RegisterRule(NewRule("failed", (state, context) => source == "condition" ? throw error : true), Allow,
                (value, state) => source == "state" ? throw error : new State(value, state));
            Assert.That(Assert.Throws<InvalidOperationException>(() => Fire()), Is.SameAs(error));
            Assert.That(calls, Is.Zero);
            Assert.That(Runtime.GetGroup("first").State.TotalStarted, Is.Zero);
            Assert.That(Runtime.GetGroup("failed").State.TotalStarted, Is.Zero);
        }

        [TestCase("throw")]
        [TestCase("fault")]
        [TestCase("null")]
        [TestCase("state")]
        public void Fire_ExecutionFailuresDoNotPreventLaterGroups(string source)
        {
            var error = new InvalidOperationException("execution phase");
            EcaExecution failed = null;
            var calls = 0;
            RegisterRule(NewRule("failed", run: (state, context) =>
            {
                if (source == "throw") throw error;
                return source == "null" ? null : Task.FromException(error);
            }), Allow, (value, state) =>
            {
                failed = Runtime.GetGroup("failed").Executions[0];
                if (source == "state") throw error;
                return new State(value, state);
            });
            RegisterRule(NewRule("next", run: (state, context) => { calls++; return Task.CompletedTask; }), Allow, CreateState);
            Fire();
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(failed.Status, Is.EqualTo(EcaExecutionStatus.Failed));
            Assert.That(failed.Exception, Is.Not.Null);
            Assert.That(Runtime.GetGroup("failed").State.TotalFinished, Is.EqualTo(1));
            Assert.That(Runtime.GetGroup("failed").Executions, Is.Empty);
            Assert.That(Runtime.GetGroup("next").State.TotalFinished, Is.EqualTo(1));
        }

        [TestCase(EcaExecutionModeOverlap.Ignore, -1, true)]
        [TestCase(EcaExecutionModeOverlap.Allow, 1, false)]
        public async Task Fire_ReentrantAdmissionIsCheckedAtOuterRunAfterInnerExecution(
            EcaExecutionModeOverlap overlap, int limit, bool keepInnerActive)
        {
            var trace = new List<string>();
            var gate = NewGate();
            RegisterRule(NewRule("A", (state, context) =>
            {
                trace.Add($"Condition A {state.EventState}");
                return state.EventState == 1;
            }, (state, context) =>
            {
                trace.Add("Action A outer");
                Fire(2);
                trace.Add("Return A outer");
                return Task.CompletedTask;
            }), Allow, CreateState);
            RegisterRule(NewRule("B", (state, context) =>
            {
                trace.Add($"Condition B {state.EventState}");
                return true;
            }, (state, context) =>
            {
                trace.Add($"Action B {state.EventState}");
                return keepInnerActive ? gate.Task : Task.CompletedTask;
            }), new EcaExecutionMode(overlap, limit), CreateState);

            Fire(1);
            Assert.That(trace, Is.EqualTo(new[]
            {
                "Condition A 1", "Condition B 1", "Action A outer",
                "Condition A 2", "Condition B 2", "Action B 2", "Return A outer"
            }));
            var group = Runtime.GetGroup("B");
            Assert.That(group.State.TotalStarted, Is.EqualTo(1));
            Assert.That(group.Executions.Count, Is.EqualTo(keepInnerActive ? 1 : 0));
            Assert.That(Runtime.GetGroup("A").State.TotalFinished, Is.EqualTo(1));
            gate.Complete();
            await WaitUntil(() => group.Executions.Count == 0);
            Assert.That(group.State.TotalFinished, Is.EqualTo(1));
        }

        [TestCase(EcaExecutionModeOverlap.Ignore, -1, 1)]
        [TestCase(EcaExecutionModeOverlap.Allow, 1, 1)]
        [TestCase(EcaExecutionModeOverlap.Allow, -1, 2)]
        public void Fire_ReentrantActionSeesActiveExecutionAndStartedCounter(
            EcaExecutionModeOverlap overlap, int limit, int expected)
        {
            var observedActive = new List<int>();
            var observedStarted = new List<long>();
            RegisterRule(NewRule(run: (state, context) =>
            {
                observedActive.Add(Runtime.GetGroup("rule").Executions.Count);
                observedStarted.Add(state.ExecutionGroupState.TotalStarted);
                if (state.EventState == 1) Fire(2);
                return Task.CompletedTask;
            }), new EcaExecutionMode(overlap, limit), CreateState);
            Fire();
            Assert.That(observedActive.Count, Is.EqualTo(expected));
            Assert.That(observedActive[0], Is.EqualTo(1));
            Assert.That(observedStarted[0], Is.EqualTo(1));
            if (expected == 2)
            {
                Assert.That(observedActive[1], Is.EqualTo(2));
                Assert.That(observedStarted[1], Is.EqualTo(2));
            }
            Assert.That(Runtime.GetGroup("rule").State.TotalStarted, Is.EqualTo(expected));
            Assert.That(Runtime.GetGroup("rule").State.TotalFinished, Is.EqualTo(expected));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Fire_PassedGroupSurvivesUnregisterAndOptionalReplacement(bool replace)
        {
            var trace = new List<string>();
            var next = NewRule("B", (state, context) => { trace.Add("Check B"); return true; },
                (state, context) => { trace.Add("Old B"); return Task.CompletedTask; });
            var removed = false;
            RegisterRule(NewRule("A", run: (state, context) =>
            {
                trace.Add("A");
                if (!removed)
                {
                    removed = Runtime.Unregister(next);
                    if (replace) RegisterRule(NewRule("B", run: (s, c) =>
                    {
                        trace.Add("New B");
                        return Task.CompletedTask;
                    }), Allow, CreateState);
                }
                return Task.CompletedTask;
            }), Allow, CreateState);
            RegisterRule(next, Allow, CreateState);
            var oldGroup = Runtime.GetGroup("B");
            Fire();
            Assert.That(removed, Is.True);
            Assert.That(trace, Is.EqualTo(new[] { "Check B", "A", "Old B" }));
            Assert.That(oldGroup.State.TotalFinished, Is.EqualTo(1));
            trace.Clear();
            Fire();
            Assert.That(trace, Is.EqualTo(replace ? new[] { "A", "New B" } : new[] { "A" }));
            Assert.That(oldGroup.State.TotalStarted, Is.EqualTo(1));
            Assert.That(Runtime.TryGetGroup("B", out _), Is.EqualTo(replace));
        }

        [Test]
        public void ForceFire_UsesBaseBarrierAndIgnoresExecutionModeAndLifecycle()
        {
            var trace = new List<string>();
            var forceState = new EcaExecutionGroupState();
            var created = 0;
            foreach (var id in new[] { "A", "B", "C" })
            {
                RegisterRule(NewRule(id, (state, context) =>
                {
                    trace.Add("Condition " + id);
                    return id != "B";
                }, (state, context) =>
                {
                    trace.Add("Action " + id);
                    return Task.CompletedTask;
                }), new EcaExecutionMode(EcaExecutionModeOverlap.Ignore, 0),
                    (value, state) => throw new Exception("Execution state creator must not be used"));
            }
            Runtime.ForceFire<int, State>(Event, 42, (rule, value) =>
            {
                created++;
                return new State(value, forceState) { RuleId = rule.Id };
            }, Conditions, Actions);
            Assert.That(created, Is.EqualTo(3));
            Assert.That(trace, Is.EqualTo(new[] { "Condition A", "Condition B", "Condition C", "Action A", "Action C" }));
            foreach (var id in new[] { "A", "B", "C" })
            {
                var group = Runtime.GetGroup(id);
                Assert.That(group.State.TotalStarted, Is.Zero);
                Assert.That(group.State.TotalFinished, Is.Zero);
                Assert.That(group.Executions, Is.Empty);
            }
        }

        [Test]
        public void ForceFire_AcceptsBaseOnlyTypesAndSharedRegistry()
        {
            var called = false;
            var rule = new BaseTestSupport.Rule
            {
                Event = Event,
                Action = new BaseTestSupport.Action { RunHandler = (state, context) => { called = true; return Task.CompletedTask; } }
            };
            Rules.Register(rule);
            Runtime.ForceFire<int, BaseTestSupport.State>(
                Event, 1, (matching, value) => new BaseTestSupport.State { RuleId = matching.Id, EventState = value },
                new BaseTestSupport.ConditionContext(), new BaseTestSupport.ActionContext());
            Assert.That(called, Is.True);
            Assert.That(Runtime.TryGetGroup(rule.Id, out _), Is.False);
        }

        [Test]
        public void Fire_PreservesGenericExtensibilityForOtherPayloadAndContexts()
        {
            var payload = new object();
            var evt = new BaseTestSupport.Event<object> { Id = "object" };
            Events.Register(evt);
            EcaExecutionRuleState<object> seen = null;
            var rule = new Rule<object, EcaExecutionRuleState<object>>
            {
                Event = evt,
                Action = new ExecutionTestSupport.Action<EcaExecutionRuleState<object>, ActionContext>
                {
                    Handler = (state, context) => { seen = state; return Task.CompletedTask; }
                }
            };
            Runtime.Register(rule, Allow, (value, state) => new EcaExecutionRuleState<object>(rule.Id, value, state));
            Runtime.Fire<object>(evt, payload, Conditions, Actions);
            Assert.That(seen.EventState, Is.SameAs(payload));
            Assert.That(seen.ExecutionGroupState, Is.SameAs(Runtime.GetGroup(rule.Id).State));
            Assert.That(seen.ExecutionGroupState.TotalFinished, Is.EqualTo(1));
            // R is still checked by caller-state Base Fire, but is no longer a normal Fire parameter.
            Assert.Throws<InvalidOperationException>(() => Runtime.ForceFire<object, IEcaExecutionRuleState<object>>(
                evt, payload, (r, value) => new EcaExecutionRuleState<object>(r.Id, value, new EcaExecutionGroupState()), Conditions, Actions));
        }

        [Test]
        public void Fire_ValidatesEventAndAllowsEmptySelection()
        {
            Fire();
            Assert.Throws<ArgumentNullException>(() => Runtime.Fire<int>(
                null, 1, Conditions, Actions));
            Assert.Throws<InvalidOperationException>(() => Runtime.Fire<int>(
                new BaseTestSupport.Event<int> { Id = "missing" }, 1, Conditions, Actions));
        }
    }
}
