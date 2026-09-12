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
            var runner = new RecordingRuleRunner();
            using var f = new BaseFixture(runner);
            var evt = f.Event<int>("event");
            var other = f.Event<string>("other");
            var rule = new EcaRule<int>("r", "Rule", evt, new TestAction<EcaRuleState<int>>((_, __) => Task.CompletedTask));
            f.Engine.Register(rule);
            f.Rule("other.rule", other, _ => { });
            f.Dispatcher.Fire(evt, 3);
            var run = runner.LastRun;
            Assert.That(runner.Check(run), Is.True);
            Assert.That(runner.RunAction(run), Is.SameAs(Task.CompletedTask));
            Assert.Throws<ArgumentException>(() => new EcaRuleRunner().Check(run));
            Assert.Throws<ArgumentException>(() => new EcaRuleRunner().RunAction(run));
            Assert.Throws<ArgumentException>(() => runner.Check(new ForeignRun()));
            Assert.Throws<ArgumentNullException>(() => runner.Check(null));
            f.Dispatcher.Fire(other, "wrong");
            Assert.Throws<ArgumentException>(() => runner.CreateRun(rule, runner.LastOccurrence));
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
                ProbeState<int> checkedState = null;
                f.Engine.Register(new EcaRule<int, ProbeState<int>, IEcaConditionRunnerContext, IEcaActionRunnerContext>(
                    id, id, evt, new TestAction<ProbeState<int>>((s, c) =>
                    {
                        Assert.That(s, Is.SameAs(checkedState));
                        Assert.That(s.EventState, Is.EqualTo(8));
                        Assert.That(c, Is.SameAs(runner.ActionInfrastructure));
                        trace.Add("Action" + id);
                        return Task.CompletedTask;
                    }), new TestCondition<ProbeState<int>>((s, c) =>
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
        public void UnsupportedStateSpecialization_FailsDuringCanonicalRegister()
        {
            using var f = new BaseFixture();
            var evt = f.Event<int>("event");
            var checks = 0;
            f.Rule("base", evt, _ => { }, _ => { checks++; return true; });
            var unsupported = new EcaRule<int, ProbeState<int>, IEcaConditionRunnerContext, IEcaActionRunnerContext>(
                "derived", "Derived", evt, new TestAction<ProbeState<int>>((_, __) => Task.CompletedTask));
            Assert.Throws<ArgumentException>(() => f.Engine.Register(unsupported));
            Assert.That(f.Rules.GetByEvent(evt).Count, Is.EqualTo(1));
            Assert.That(checks, Is.Zero);
        }

        private sealed class ForeignRun : IEcaRuleRun { }
        private sealed class ConditionContext : IEcaConditionRunnerContext { }
        private sealed class ActionContext : IEcaActionRunnerContext { }
        private sealed class ProbeState<T> : EcaRuleState<T>
        {
            internal ProbeState(T eventState) : base(eventState) { }
        }

        private sealed class RecordingRuleRunner : EcaRuleRunner
        {
            internal IEcaRuleRun LastRun;
            internal EcaEventOccurrence LastOccurrence;
            public override IEcaRuleRun CreateRun(IEcaRule rule, EcaEventOccurrence occurrence)
            {
                LastOccurrence = occurrence;
                return LastRun = base.CreateRun(rule, occurrence);
            }
        }

        /* Внешний производный runner расширяет State, используя тот же Base bridge. */
        private sealed class ProbeRunner : EcaRuleRunner
        {
            private readonly List<string> _trace;
            internal readonly IEcaConditionRunnerContext ConditionInfrastructure = new ConditionContext();
            internal readonly IEcaActionRunnerContext ActionInfrastructure = new ActionContext();
            internal ProbeRunner(List<string> trace) { _trace = trace; }

            protected override void ValidateTyped<T>(IEcaRule rule)
            {
                if (!(rule is IEcaRule<T, ProbeState<T>, IEcaConditionRunnerContext, IEcaActionRunnerContext>))
                    throw new ArgumentException("Unsupported probe rule.", nameof(rule));
            }

            protected override IEcaRuleRun CreateTyped<T>(IEcaRule rule, IEcaEvent<T> ecaEvent, T eventState)
            {
                _trace.Add("Create" + rule.Id);
                var typed = (IEcaRule<T, ProbeState<T>, IEcaConditionRunnerContext, IEcaActionRunnerContext>)rule;
                return CreateRunCore(typed, new ProbeState<T>(eventState), ConditionInfrastructure, ActionInfrastructure);
            }
        }
    }
}
