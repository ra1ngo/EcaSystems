using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EcaSystems.Core1;
using NUnit.Framework;

namespace EcaSystems.Tests.Core1
{
    [TestFixture]
    public sealed class EcaRuleRunnerTests
    {
        [Test]
        public void Runner_RejectsForeignTokensAndMismatchedRules()
        {
            var runner = new EcaRuleRunner();
            using var f = new BaseFixture();
            var evt = f.Event<int>("event");
            var other = f.Event<string>("other");
            var rule = new EcaRule<int>("r", "Rule", evt, new TestAction<EcaRuleState<int>>((_, __) => Task.CompletedTask));
            EcaEventOccurrence occurrence = null;
            f.Dispatcher.Fired += value => occurrence = value;
            f.Dispatcher.Fire(evt, 3);
            var run = runner.CreateRun(rule, occurrence);
            Assert.That(runner.Check(run), Is.True);
            Assert.That(runner.RunAction(run), Is.SameAs(Task.CompletedTask));
            Assert.Throws<ArgumentException>(() => new EcaRuleRunner().Check(run));
            Assert.Throws<ArgumentException>(() => new EcaRuleRunner().RunAction(run));
            Assert.Throws<ArgumentException>(() => runner.Check(new ForeignRun()));
            Assert.Throws<ArgumentNullException>(() => runner.Check(null));
            f.Dispatcher.Fire(other, "wrong");
            Assert.Throws<ArgumentException>(() => runner.CreateRun(rule, occurrence));
        }

        [Test]
        public void IndependentRunners_AcceptBroaderStateContractsByContravariance()
        {
            var state = new EcaRuleState<string>("payload");
            var conditionContext = new ConditionContext();
            var actionContext = new ActionContext();
            var condition = new TestCondition<IEcaRuleState>((s, c) =>
            {
                Assert.That(s, Is.SameAs(state));
                Assert.That(c, Is.SameAs(conditionContext));
                return true;
            });
            var action = new TestAction<IEcaRuleState>((s, c) =>
            {
                Assert.That(s, Is.SameAs(state));
                Assert.That(c, Is.SameAs(actionContext));
                return Task.CompletedTask;
            });
            IEcaCondition<EcaRuleState<string>, ConditionContext> narrowedCondition = condition;
            IEcaAction<EcaRuleState<string>, ActionContext> narrowedAction = action;
            Assert.That(new EcaConditionRunner().Check(narrowedCondition, state, conditionContext), Is.True);
            Assert.That(new EcaActionRunner().Run(narrowedAction, state, actionContext).IsCompleted, Is.True);
            IEcaRuleState<object> covariantState = state;
            Assert.That(covariantState.EventState, Is.EqualTo("payload"));
        }

        [Test]
        public void ReplacementRunner_ReusesEngineWithDerivedStateAndOwnInfrastructure()
        {
            var trace = new List<string>();
            var runner = new ProbeRunner(trace);
            using var f = new BaseFixture(runner);
            var evt = f.Event<int>("event");
            for (var i = 1; i <= 2; i++)
            {
                var id = i.ToString();
                ProbeState checkedState = null;
                f.Rules.Register(new EcaRule<int, ProbeState, IEcaConditionRunnerContext, IEcaActionRunnerContext>(
                    id, id, evt, new TestAction<ProbeState>((s, c) =>
                    {
                        Assert.That(s, Is.SameAs(checkedState));
                        Assert.That(s.EventState, Is.EqualTo(8));
                        Assert.That(c, Is.SameAs(runner.ActionInfrastructure));
                        trace.Add("Action" + id);
                        return Task.CompletedTask;
                    }), new TestCondition<ProbeState>((s, c) =>
                    {
                        checkedState = s;
                        Assert.That(c, Is.SameAs(runner.ConditionInfrastructure));
                        trace.Add("Check" + id);
                        return true;
                    })));
            }
            f.Dispatcher.Fire(evt, 8);
            CollectionAssert.AreEqual(new[] { "Create1", "Create2", "Check1", "Check2", "Action1", "Action2" }, trace);
        }

        [Test]
        public void UnsupportedStateSpecialization_FailsDuringCreateBeforeAnyCondition()
        {
            using var f = new BaseFixture();
            var evt = f.Event<int>("event");
            var checks = 0;
            f.Rule("base", evt, _ => Assert.Fail("No action expected"), _ => { checks++; return true; });
            f.Rules.Register(new EcaRule<int, ProbeState, IEcaConditionRunnerContext, IEcaActionRunnerContext>(
                "derived", "Derived", evt, new TestAction<ProbeState>((_, __) => Task.CompletedTask)));
            Assert.Throws<ArgumentException>(() => f.Dispatcher.Fire(evt, 0));
            Assert.That(checks, Is.Zero);
        }

        private sealed class ForeignRun : IEcaRuleRun { }
        private sealed class ConditionContext : IEcaConditionRunnerContext { }
        private sealed class ActionContext : IEcaActionRunnerContext { }
        private sealed class ProbeState : EcaRuleState<int>
        {
            internal ProbeState(int eventState) : base(eventState) { }
        }

        /* A test-only external runner proves the public seam works from a separate
           assembly. It deliberately supports one concrete state, no Execution model. */
        private sealed class ProbeRunner : IEcaRuleRunner
        {
            private readonly List<string> _trace;
            internal readonly IEcaConditionRunnerContext ConditionInfrastructure = new ConditionContext();
            internal readonly IEcaActionRunnerContext ActionInfrastructure = new ActionContext();
            internal ProbeRunner(List<string> trace) { _trace = trace; }

            public IEcaRuleRun CreateRun(IEcaRule rule, EcaEventOccurrence occurrence)
            {
                _trace.Add("Create" + rule.Id);
                return new ProbeRun
                {
                    Rule = (IEcaRule<int, ProbeState, IEcaConditionRunnerContext, IEcaActionRunnerContext>)rule,
                    State = new ProbeState(occurrence.Accept(new IntReader()))
                };
            }

            public bool Check(IEcaRuleRun run)
            {
                var data = (ProbeRun)run;
                return data.Rule.Condition == null || new EcaConditionRunner().Check(data.Rule.Condition, data.State, ConditionInfrastructure);
            }

            public Task RunAction(IEcaRuleRun run)
            {
                var data = (ProbeRun)run;
                return new EcaActionRunner().Run(data.Rule.Action, data.State, ActionInfrastructure);
            }

            private sealed class ProbeRun : IEcaRuleRun
            {
                internal IEcaRule<int, ProbeState, IEcaConditionRunnerContext, IEcaActionRunnerContext> Rule;
                internal ProbeState State;
            }

            private sealed class IntReader : IEcaEventOccurrenceVisitor<int>
            {
                public int Visit<T>(IEcaEvent<T> ecaEvent, T eventState)
                {
                    if (typeof(T) != typeof(int)) throw new ArgumentException("Probe supports int only.");
                    return (int)(object)eventState;
                }
            }
        }
    }
}
