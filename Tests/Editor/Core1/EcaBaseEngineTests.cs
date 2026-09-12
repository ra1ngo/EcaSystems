using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EcaSystems.Core1;
using NUnit.Framework;

namespace EcaSystems.Tests.Core1
{
    [TestFixture]
    public sealed class EcaBaseEngineTests
    {
        [Test]
        public void OneEngine_HandlesReferenceStructNullableAndEmptyEventStates()
        {
            using var f = new BaseFixture();
            var reference = f.Event<Payload>("reference");
            var number = f.Event<int>("number");
            var nullable = f.Event<int?>("nullable");
            var empty = new EcaEvent("empty", "Empty");
            f.Events.Register(empty);
            var trace = new List<string>();
            var payload = new Payload { Value = 7 };
            f.Rule("r1", reference, s => { Assert.That(s.EventState, Is.SameAs(payload)); trace.Add("reference"); });
            f.Rule("r2", number, s => trace.Add("int:" + s.EventState), s => s.EventState > 0);
            f.Rule("r3", nullable, s => { Assert.That(s.EventState, Is.Null); trace.Add("nullable"); });
            f.Engine.Register(new EcaRule("r4", "Empty rule", empty,
                new TestAction<EcaRuleState<EcaEventStateEmpty>>((s, _) =>
                {
                    Assert.That(s.EventState, Is.EqualTo(default(EcaEventStateEmpty)));
                    trace.Add("empty");
                    return Task.CompletedTask;
                })));
            f.Dispatcher.Fire(reference, payload);
            f.Dispatcher.Fire(number, 42);
            f.Dispatcher.Fire(nullable, null);
            f.Dispatcher.Fire(empty);
            f.Dispatcher.Fire(number, -1);
            CollectionAssert.AreEqual(new[] { "reference", "int:42", "nullable", "empty" }, trace);
        }

        [Test]
        public void Fire_AllConditionsPrecedeAllActions_RegardlessOfActionMutation()
        {
            using var f = new BaseFixture();
            var evt = f.Event<int>("event");
            var trace = new List<string>();
            var flag = false;
            f.Rule("one", evt, _ => { trace.Add("Action1"); flag = true; }, _ => { trace.Add("Check1"); return !flag; });
            f.Rule("two", evt, _ => trace.Add("Action2"), _ => { trace.Add("Check2"); return !flag; });
            f.Dispatcher.Fire(evt, 1);
            CollectionAssert.AreEqual(new[] { "Check1", "Check2", "Action1", "Action2" }, trace);
        }

        [Test]
        public void RuleState_IsSharedAcrossRoles_ButFreshForEachRuleAndFire()
        {
            using var f = new BaseFixture();
            var evt = f.Event<Payload>("event");
            var checkedStates = new List<EcaRuleState<Payload>>();
            var actionStates = new List<EcaRuleState<Payload>>();
            for (var i = 0; i < 2; i++)
                f.Rule("r" + i, evt, s => actionStates.Add(s), s => { checkedStates.Add(s); return true; });
            var payload = new Payload();
            f.Dispatcher.Fire(evt, payload);
            f.Dispatcher.Fire(evt, payload);
            Assert.That(checkedStates.Count, Is.EqualTo(4));
            for (var i = 0; i < 4; i++)
            {
                Assert.That(actionStates[i], Is.SameAs(checkedStates[i]));
                Assert.That(actionStates[i].EventState, Is.SameAs(payload));
                for (var j = 0; j < i; j++) Assert.That(checkedStates[i], Is.Not.SameAs(checkedStates[j]));
            }
        }

        [Test]
        public void NestedFire_RunsWholeSynchronousBPipelineBeforeActionAContinues()
        {
            using var f = new BaseFixture();
            var a = f.Event<int>("a");
            var b = f.Event<string>("b");
            var trace = new List<string>();
            f.Rule("a1", a, _ =>
            {
                trace.Add("Action A starts");
                f.Dispatcher.Fire(b, "nested");
                trace.Add("Action A continues");
            }, _ => { trace.Add("Check A1"); return true; });
            f.Rule("a2", a, _ => trace.Add("Action A2"), _ => { trace.Add("Check A2"); return true; });
            f.Rule("b1", b, s => trace.Add("Action B1:" + s.EventState), _ => { trace.Add("Check B1"); return true; });
            f.Rule("b2", b, _ => trace.Add("Action B2"), _ => { trace.Add("Check B2"); return true; });
            f.Dispatcher.Fire(a, 1);
            CollectionAssert.AreEqual(new[] { "Check A1", "Check A2", "Action A starts", "Check B1", "Check B2",
                "Action B1:nested", "Action B2", "Action A continues", "Action A2" }, trace);
        }

        [Test]
        public void SameEvent_ReentrantFireKeepsOuterStateAndRemainingActions()
        {
            using var f = new BaseFixture();
            var evt = f.Event<int>("recursive");
            var trace = new List<string>();
            EcaRuleState<int> outer = null;
            f.Rule("one", evt, s =>
            {
                trace.Add("start:" + s.EventState);
                if (s.EventState == 0)
                {
                    outer = s;
                    f.Dispatcher.Fire(evt, 1);
                    Assert.That(s, Is.SameAs(outer));
                    Assert.That(s.EventState, Is.Zero);
                }
                else Assert.That(s, Is.Not.SameAs(outer));
                trace.Add("end:" + s.EventState);
            });
            f.Rule("two", evt, s => trace.Add("second:" + s.EventState));
            f.Dispatcher.Fire(evt, 0);
            CollectionAssert.AreEqual(new[] { "start:0", "start:1", "end:1", "second:1", "end:0", "second:0" }, trace);
        }

        [Test]
        public void NestedFireFromCondition_InvariantIsPerInvocation()
        {
            using var f = new BaseFixture();
            var a = f.Event<int>("a");
            var b = f.Event<int>("b");
            var trace = new List<string>();
            f.Rule("a1", a, _ => trace.Add("Action A1"), _ =>
            {
                trace.Add("Check A1 starts");
                f.Dispatcher.Fire(b, 0);
                trace.Add("Check A1 continues");
                return true;
            });
            f.Rule("a2", a, _ => trace.Add("Action A2"), _ => { trace.Add("Check A2"); return true; });
            f.Rule("b", b, _ => trace.Add("Action B"), _ => { trace.Add("Check B"); return true; });
            f.Dispatcher.Fire(a, 0);
            CollectionAssert.AreEqual(new[] { "Check A1 starts", "Check B", "Action B", "Check A1 continues",
                "Check A2", "Action A1", "Action A2" }, trace);
        }

        [Test]
        public void ConditionException_PreventsAllActions_AndNextFireStillWorks()
        {
            using var f = new BaseFixture();
            var evt = f.Event<int>("event");
            var actions = 0;
            var fail = true;
            f.Rule("one", evt, _ => actions++);
            f.Rule("two", evt, _ => actions++, _ => fail ? throw new InvalidOperationException("check") : true);
            Assert.Throws<InvalidOperationException>(() => f.Dispatcher.Fire(evt, 0));
            Assert.That(actions, Is.Zero);
            fail = false;
            f.Dispatcher.Fire(evt, 0);
            Assert.That(actions, Is.EqualTo(2));
        }

        [Test]
        public void SynchronousActionException_StopsRemainingActions()
        {
            using var f = new BaseFixture();
            var evt = f.Event<int>("event");
            var trace = new List<string>();
            f.Rule("one", evt, _ => throw new InvalidOperationException("action"), _ => { trace.Add("Check1"); return true; });
            f.Rule("two", evt, _ => trace.Add("Action2"), _ => { trace.Add("Check2"); return true; });
            Assert.Throws<InvalidOperationException>(() => f.Dispatcher.Fire(evt, 0));
            CollectionAssert.AreEqual(new[] { "Check1", "Check2" }, trace);
        }

        [Test]
        public async Task AsyncAction_FireDoesNotAwaitAndStateSurvivesUntilCompletion()
        {
            using var f = new BaseFixture();
            var evt = f.Event<int>("event");
            var release = new TaskCompletionSource<bool>();
            var trace = new List<string>();
            EcaRuleState<int> checkedState = null;
            Task running = null;
            async Task Run(EcaRuleState<int> state)
            {
                trace.Add("started");
                await release.Task;
                Assert.That(state, Is.SameAs(checkedState));
                Assert.That(state.EventState, Is.EqualTo(9));
                trace.Add("completed");
            }
            f.Engine.Register(new EcaRule<int>("async", "Async", evt,
                new TestAction<EcaRuleState<int>>((s, _) => running = Run(s)),
                new TestCondition<EcaRuleState<int>>((s, _) => { checkedState = s; return true; })));
            f.Rule("next", evt, _ => trace.Add("next"));
            try
            {
                f.Dispatcher.Fire(evt, 9);
                Assert.That(running.IsCompleted, Is.False);
                CollectionAssert.AreEqual(new[] { "started", "next" }, trace);
            }
            finally { release.SetResult(true); }
            await running;
            CollectionAssert.AreEqual(new[] { "started", "next", "completed" }, trace);
        }

        [Test]
        public void FaultedTask_DoesNotBecomeSynchronousFireFailure()
        {
            using var f = new BaseFixture();
            var evt = f.Event<int>("event");
            var failed = Task.FromException(new InvalidOperationException("async failure"));
            var ran = false;
            f.Engine.Register(new EcaRule<int>("failed", "Failed", evt,
                new TestAction<EcaRuleState<int>>((_, __) => failed)));
            f.Rule("next", evt, _ => ran = true);
            Assert.DoesNotThrow(() => f.Dispatcher.Fire(evt, 0));
            Assert.That(ran, Is.True);
            Assert.That(failed.Exception.InnerException.Message, Is.EqualTo("async failure"));
        }

        [Test]
        public void RegistryChangesDuringCheck_AffectNextFireOnly()
        {
            using var f = new BaseFixture();
            var evt = f.Event<int>("event");
            var trace = new List<string>();
            EcaRule<int> removed = null;
            var changed = false;
            f.Rule("one", evt, _ => trace.Add("one"), _ =>
            {
                if (!changed)
                {
                    changed = true;
                    f.Engine.Unregister(removed);
                    f.Rule("new", evt, s => trace.Add("new"));
                }
                return true;
            });
            removed = f.Rule("removed", evt, _ => trace.Add("removed"));
            f.Dispatcher.Fire(evt, 0);
            f.Dispatcher.Fire(evt, 0);
            CollectionAssert.AreEqual(new[] { "one", "removed", "one", "new" }, trace);
        }

        [Test]
        public void Dispose_UnsubscribesEngineWithoutClosingDispatcher()
        {
            using var f = new BaseFixture();
            var evt = f.Event<int>("event");
            var actions = 0;
            f.Rule("r", evt, _ => actions++);
            f.Dispatcher.Fire(evt, 0);
            f.Engine.Dispose();
            f.Engine.Dispose();
            f.Dispatcher.Fire(evt, 0);
            Assert.That(actions, Is.EqualTo(1));
            using var replacement = new EcaBaseEngine(f.Dispatcher, f.Rules, new EcaRuleRunner());
            f.Dispatcher.Fire(evt, 0);
            Assert.That(actions, Is.EqualTo(2));
        }

        private sealed class Payload { internal int Value; }
    }
}
