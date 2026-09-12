using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using EcaSystems.Core1;
using NUnit.Framework;
using static EcaSystems.Tests.Core1.LayerRules;

namespace EcaSystems.Tests.Core1
{
    [TestFixture]
    public sealed class EcaScopeExtensionTests
    {
        [TestCase(EcaOverlap.Ignore, 1)]
        [TestCase(EcaOverlap.Allow, 2)]
        public async Task UpperLayer_UsesScopeCompositionAndInheritedPipeline(EcaOverlap overlap, int active)
        {
            var events = new EcaEventRegistry();
            var evt = Event<int>(events);
            var trace = new List<string>();
            var runners = new Dictionary<string, ProbeScopeRuleRunner>();
            using var owner = CreateOwner(events, (groups, state) =>
            {
                var runner = new ProbeScopeRuleRunner(groups, state, trace);
                runners.Add(state.ScopeId, runner);
                return runner;
            });
            var root = owner.CreateScope("root");
            var child = root.CreateScope("child");
            var grandchild = child.CreateScope("grandchild");
            Assert.That(runners.Count, Is.EqualTo(3));
            Assert.That(child.ParentScopeId, Is.EqualTo(root.ScopeId));
            Assert.That(grandchild.ParentScopeId, Is.EqualTo(child.ScopeId));
            Assert.That(runners["child"].Groups, Is.Not.SameAs(runners["root"].Groups));

            using var gate = new ActionGate<ProbeScopeRuleState<int>>();
            var checkedStates = new List<ProbeScopeRuleState<int>>();
            var actionStates = new List<ProbeScopeRuleState<int>>();
            var conditionContexts = new List<ProbeConditionRunnerContext>();
            var actionContexts = new List<ProbeActionRunnerContext>();
            var startsAtCheck = new List<long>();
            for (var i = 1; i <= 2; i++)
            {
                var id = "r" + i;
                var rule = new EcaRule<int, ProbeScopeRuleState<int>, ProbeConditionRunnerContext, ProbeActionRunnerContext>(
                    id, id, evt, new ProbeAction<int>((state, context) =>
                    {
                        trace.Add("Action " + id);
                        actionStates.Add(state);
                        actionContexts.Add(context);
                        return id == "r1" ? gate.Run(state) : Task.CompletedTask;
                    }), new ProbeCondition<int>((state, context) =>
                    {
                        trace.Add("Check " + id);
                        checkedStates.Add(state);
                        conditionContexts.Add(context);
                        startsAtCheck.Add(runners[state.ScopeState.ScopeId].Groups.Get("r1").State.TotalStarted);
                        return true;
                    }));
                Register(root, rule, new EcaExecutionMode(overlap, 2));
                Register(child, rule, new EcaExecutionMode(overlap, 2));
            }

            root.Fire(evt, 42);
            CollectionAssert.AreEqual(new[] { "Create r1", "Create r2", "Check r1", "Check r2", "Action r1", "Action r2" }, trace);
            CollectionAssert.AreEqual(new long[] { 0, 0 }, startsAtCheck);
            Assert.That(actionStates.Count, Is.EqualTo(2));
            for (var i = 0; i < 2; i++)
            {
                Assert.That(actionStates[i], Is.SameAs(checkedStates[i]));
                Assert.That(actionStates[i].GetType(), Is.EqualTo(typeof(ProbeScopeRuleState<int>)));
                Assert.That(actionStates[i].EventState, Is.EqualTo(42));
                Assert.That(actionStates[i].ScopeState, Is.SameAs(root.State));
                Assert.That(conditionContexts[i], Is.SameAs(runners["root"].ConditionContext));
                Assert.That(actionContexts[i], Is.SameAs(runners["root"].ActionContext));
            }
            var rootGroup = runners["root"].Groups.Get("r1");
            var childGroup = runners["child"].Groups.Get("r1");
            Assert.That(rootGroup.State, Is.SameAs(actionStates[0].RuleExecutionGroupState));
            Assert.That(childGroup.State.TotalStarted, Is.Zero, "Fire root остаётся локальным.");
            var execution = rootGroup.Executions[0];
            root.Fire(evt, 43);
            Assert.That(rootGroup.Executions.Count, Is.EqualTo(active));
            Assert.That(rootGroup.State.TotalStarted, Is.EqualTo(active));
            Assert.That(checkedStates.Count, Is.EqualTo(4));
            gate.Dispose();
            await WaitUntil(() => rootGroup.Executions.Count == 0);
            Assert.That(execution.Status, Is.EqualTo(EcaRuleExecutionStatus.Completed));
            root.Fire(evt, 44);
            gate.Dispose();
            await WaitUntil(() => rootGroup.State.TotalFinished == 2);
            root.Fire(evt, 45);
            Assert.That(rootGroup.State.TotalStarted, Is.EqualTo(2), "Унаследованный Limit ограничивает старты.");
            Assert.That(rootGroup.Executions, Is.Empty);

            child.Fire(evt, 46);
            Assert.That(childGroup.State.TotalStarted, Is.EqualTo(1));
            Assert.That(gate.States[gate.States.Count - 1].ScopeState, Is.SameAs(child.State));
            root.Dispose();
            Assert.That(root.IsDisposed && child.IsDisposed && grandchild.IsDisposed, Is.True);
            Assert.That(owner.ScopeCount, Is.Zero);
            gate.Dispose();
            await WaitUntil(() => childGroup.State.TotalFinished == 1);
            Assert.That(events.IsRegistered(evt), Is.True);
        }

        /* Reflection нужна только для доступа тестовой assembly к internal seams.
           Runtime pipeline и общий type-erasure bridge исполняются без reflection. */
        private static EcaScopeEngine CreateOwner(EcaEventRegistry events,
            Func<EcaRuleExecutionRegistry, EcaScopeState, EcaScopeRuleRunner> factory)
            => (EcaScopeEngine)Activator.CreateInstance(typeof(EcaScopeEngine), BindingFlags.Instance | BindingFlags.NonPublic,
                null, new object[] { events, factory }, null);

        private static void Register<T>(EcaScope scope,
            IEcaRule<T, ProbeScopeRuleState<T>, ProbeConditionRunnerContext, ProbeActionRunnerContext> rule, EcaExecutionMode mode)
        {
            typeof(EcaScope).GetMethod("RegisterCore", BindingFlags.Instance | BindingFlags.NonPublic)
                .MakeGenericMethod(typeof(T), typeof(ProbeScopeRuleState<T>), typeof(ProbeConditionRunnerContext), typeof(ProbeActionRunnerContext))
                .Invoke(scope, new object[] { rule, mode });
        }

        private sealed class ProbeScopeRuleState<T> : EcaScopeRuleState<T>
        {
            internal ProbeScopeRuleState(T payload, EcaRuleExecutionGroupState group, EcaScopeState scope) : base(payload, group, scope) { }
        }

        private sealed class ProbeConditionRunnerContext : IEcaScopeConditionRunnerContext { }
        private sealed class ProbeActionRunnerContext : IEcaScopeActionRunnerContext { }

        private sealed class ProbeScopeRuleRunner : EcaScopeRuleRunner
        {
            internal readonly EcaRuleExecutionRegistry Groups;
            internal readonly ProbeConditionRunnerContext ConditionContext = new();
            internal readonly ProbeActionRunnerContext ActionContext = new();
            private readonly EcaScopeState _scopeState;
            private readonly List<string> _trace;
            internal ProbeScopeRuleRunner(EcaRuleExecutionRegistry groups, EcaScopeState scopeState, List<string> trace) : base(groups, scopeState)
            {
                Groups = groups;
                _scopeState = scopeState;
                _trace = trace;
            }

            protected override void ValidateTyped<T>(IEcaRule rule)
            {
                if (!(rule is IEcaRule<T, ProbeScopeRuleState<T>, ProbeConditionRunnerContext, ProbeActionRunnerContext>))
                    throw new ArgumentException("Unsupported probe specialization.", nameof(rule));
            }

            protected override IEcaRuleRun CreateTyped<T>(IEcaRule rule, IEcaEvent<T> evt, T payload)
            {
                _trace.Add("Create " + rule.Id);
                var typed = (IEcaRule<T, ProbeScopeRuleState<T>, ProbeConditionRunnerContext, ProbeActionRunnerContext>)rule;
                var group = GetGroup(rule);
                return WrapRun(CreateRunCore(typed, new ProbeScopeRuleState<T>(payload, group.State, _scopeState),
                    ConditionContext, ActionContext), group);
            }
        }

        private sealed class ProbeCondition<T> : IEcaCondition<ProbeScopeRuleState<T>, ProbeConditionRunnerContext>
        {
            private readonly Func<ProbeScopeRuleState<T>, ProbeConditionRunnerContext, bool> _check;
            public string Id => "probe.condition";
            public string Name => Id;
            public string Description => "";
            internal ProbeCondition(Func<ProbeScopeRuleState<T>, ProbeConditionRunnerContext, bool> check) { _check = check; }
            public bool Check(ProbeScopeRuleState<T> state, ProbeConditionRunnerContext context) => _check(state, context);
        }

        private sealed class ProbeAction<T> : IEcaAction<ProbeScopeRuleState<T>, ProbeActionRunnerContext>
        {
            private readonly Func<ProbeScopeRuleState<T>, ProbeActionRunnerContext, Task> _run;
            public string Id => "probe.action";
            public string Name => Id;
            public string Description => "";
            internal ProbeAction(Func<ProbeScopeRuleState<T>, ProbeActionRunnerContext, Task> run) { _run = run; }
            public Task Run(ProbeScopeRuleState<T> state, ProbeActionRunnerContext context) => _run(state, context);
        }
    }
}
