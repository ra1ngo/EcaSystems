using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EcaSystems.Core2;
using NUnit.Framework;

namespace EcaSystems.Tests.Core2
{
    public sealed class EcaRuleCreatorTests
    {
        private EcaSystemsRuntime _runtime;
        private BaseTestSupport.Event<int> _event;
        private EcaSystem _system;
        private EcaCommandRegistry _commands;
        private static EcaExecutionMode Allow => new(EcaExecutionModeOverlap.Allow);

        [SetUp]
        public void SetUp()
        {
            _runtime = new EcaSystemsRuntime();
            _event = new BaseTestSupport.Event<int>();
            var events = new EcaBaseEventRegistry();
            events.Register(_event);
            _commands = new EcaCommandRegistry();
            _system = new EcaSystem("system", new EcaSystemNamespace("ns"), events, _commands);
        }

        [TearDown] public void TearDown() => _runtime.Dispose();

        [Test]
        public void ClassRule_ResolvesCanonicalEventInitializesActionAndKeepsRegistrationSeparate()
        {
            var scope = _runtime.CreateScope();
            _runtime.ConnectSystem(_system);
            var rule = _runtime.CreateRule<int, Positive, RecordAction>("intro", _event.Id);
            Assert.That(rule.Event, Is.SameAs(_event));
            Assert.That(((IEcaRule)rule).Event, Is.SameAs(_event));
            Assert.That(((IEcaRule)rule).Action, Is.SameAs(rule.Action));
            Assert.That(((IEcaRule)rule).Condition, Is.SameAs(rule.Condition));
            Assert.That(rule.Condition, Is.TypeOf<Positive>());
            var action = (RecordAction)rule.Action;
            Assert.That(action.Capability, Is.Not.Null);
            Assert.That(scope.TryGetGroup(rule.Id, out _), Is.False);
            scope.Register(rule, Allow);
            scope.EventEmitter.Fire(_event, -1);
            Assert.That(action.Seen, Is.Empty);
            scope.EventEmitter.Fire(_event, 7);
            Assert.That(action.Seen[0].EventState, Is.EqualTo(7));
        }

        [Test]
        public void ClassActionUsesInitializedCommandsWithCurrentStateAndContext()
        {
            IEcaRuleState receivedState = null;
            IEcaActionContext receivedContext = null;
            _commands.Register(new CaptureCommand { Handler = (s, c, a) =>
            { receivedState = s; receivedContext = c; return Task.CompletedTask; } });
            _runtime.ConnectSystem(_system);
            var rule = _runtime.CreateRule<int, CommandAction>("r", _event.Id);
            var scope = _runtime.CreateScope();
            var context = new BaseTestSupport.ActionContext();
            scope.Register(rule, Allow);
            scope.Fire(_event, 19, actionContext: context);
            Assert.That(receivedState, Is.SameAs(((CommandAction)rule.Action).Seen));
            Assert.That(((EcaScopeRuleState<int>)receivedState).EventState, Is.EqualTo(19));
            Assert.That(receivedContext, Is.SameAs(context));
            Assert.That(scope.GetGroup("r").State.TotalFinished, Is.EqualTo(1));
        }

        [Test]
        public void CreationRequiresConnectedEventAndChecksDeclaredEventType()
        {
            Assert.Throws<InvalidOperationException>(() => _runtime.CreateRule<int, RecordAction>("r", _event.Id));
            _runtime.ConnectSystem(_system);
            Assert.Throws<ArgumentException>(() => _runtime.CreateRule<string>("r", _event.Id, (s, c, commands) => Task.CompletedTask));
            _runtime.DisconnectSystem(_system);
            Assert.Throws<InvalidOperationException>(() => _runtime.CreateRule<int, RecordAction>("r", _event.Id));
        }

        [Test]
        public void CreationRejectsLyingEventMetadata()
        {
            var events = new EcaBaseEventRegistry();
            events.Register(new LyingEvent());
            _runtime.ConnectSystem(new EcaSystem("liar", new EcaSystemNamespace("liar"), events, new EcaCommandRegistry()));
            Assert.Throws<ArgumentException>(() => _runtime.CreateRule<int, RecordAction>("r", "liar"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void CreationRejectsInvalidRuleId(string id)
        {
            _runtime.ConnectSystem(_system);
            Assert.Throws<ArgumentException>(() => _runtime.CreateRule<int, RecordAction>(id, _event.Id));
        }

        [Test]
        public void NoConditionRuleCanBeSharedAcrossScopesWithIndependentGroups()
        {
            _runtime.ConnectSystem(_system);
            var rule = _runtime.CreateRule<int, RecordAction>("r", _event.Id);
            Assert.That(rule.Condition, Is.Null);
            var first = _runtime.CreateScope("first");
            var second = _runtime.CreateScope("second");
            first.Register(rule, Allow);
            second.Register(rule, Allow);
            first.Fire(_event, 1);
            second.Fire(_event, 2);
            var seen = ((RecordAction)rule.Action).Seen;
            Assert.That(seen.Count, Is.EqualTo(2));
            Assert.That(seen[0].ScopeState, Is.SameAs(first.State));
            Assert.That(seen[1].ScopeState, Is.SameAs(second.State));
            Assert.That(first.GetGroup("r"), Is.Not.SameAs(second.GetGroup("r")));
            Assert.That(first.GetGroup("r").State.TotalStarted, Is.EqualTo(1));
            Assert.That(second.GetGroup("r").State.TotalStarted, Is.EqualTo(1));
            // Creator does not reserve IDs; uniqueness remains a local registration concern.
            var duplicate = _runtime.CreateRule<int, RecordAction>("r", _event.Id);
            Assert.Throws<InvalidOperationException>(() => first.Register(duplicate, Allow));
        }

        [Test]
        public void CustomStateIsSuppliedByScopeRegistrationFactory()
        {
            _runtime.ConnectSystem(_system);
            var rule = _runtime.CreateRule<int, CustomState, CustomCondition, CustomAction>("r", _event.Id);
            var scope = _runtime.CreateScope();
            var created = new List<CustomState>();
            scope.Register(rule, Allow, (execution, local) =>
            {
                var state = new CustomState { EventState = execution.EventState, ExecutionGroupState = execution.ExecutionGroupState, ScopeState = local };
                created.Add(state);
                return state;
            });
            scope.Fire(_event, 8);
            Assert.That(((CustomCondition)rule.Condition).Seen, Is.SameAs(created[0]));
            Assert.That(((CustomAction)rule.Action).Seen, Is.SameAs(created[1]));
            Assert.That(created[1].EventState, Is.EqualTo(8));
            Assert.That(created[1].ScopeState, Is.SameAs(scope.State));
        }

        [Test]
        public void ActionInitializationAllowsExactlyOneSuccessfulCall()
        {
            var action = new RecordAction();
            Assert.Throws<InvalidOperationException>(() => _ = action.Capability);
            Assert.Throws<ArgumentNullException>(() => action.Initialize(null));
            var commands = new EcaCommandRunner(new EcaCommandRegistry());
            action.Initialize(commands);
            Assert.That(action.Capability, Is.SameAs(commands));
            Assert.Throws<InvalidOperationException>(() => action.Initialize(commands));
            Assert.Throws<InvalidOperationException>(() => action.Initialize(new EcaCommandRunner(new EcaCommandRegistry())));
            Assert.That(action.Capability, Is.SameAs(commands));
        }

        [Test]
        public async Task DelegatesReceiveCurrentContextsAndStableCommandsAndTrackAsyncExecution()
        {
            var seen = new List<(IEcaRuleState state, IEcaActionContext context)>();
            var gate = new TaskCompletionSource<bool>();
            _commands.Register(new CaptureCommand { Handler = (s, c, a) => { seen.Add((s, c)); return gate.Task; } });
            _runtime.ConnectSystem(_system);
            var classRule = _runtime.CreateRule<int, RecordAction>("class", _event.Id);
            var stable = ((RecordAction)classRule.Action).Capability;
            var conditionContext = new BaseTestSupport.ConditionContext();
            var actionContext = new BaseTestSupport.ActionContext();
            IEcaConditionContext checkedContext = null;
            EcaScopeRuleState<int> checkedState = null, actionState = null;
            var rule = _runtime.CreateRule<int>(id: "delegate", eventId: _event.Id,
                condition: (state, context) => { checkedState = state; checkedContext = context; return state.EventState > 0; },
                action: (state, context, commands) =>
                {
                    Assert.That(commands, Is.SameAs(stable));
                    actionState = state;
                    return commands.Run("capture", state, context, 1);
                });
            var scope = _runtime.CreateScope();
            scope.Register(rule, Allow);
            try
            {
                scope.EventEmitter.Fire(_event, 4, conditionContext, actionContext);
                Assert.That(checkedState.EventState, Is.EqualTo(4));
                Assert.That(checkedContext, Is.SameAs(conditionContext));
                Assert.That(seen[0].state, Is.SameAs(actionState));
                Assert.That(seen[0].context, Is.SameAs(actionContext));
                Assert.That(scope.GetGroup(rule.Id).Executions[0].Status, Is.EqualTo(EcaExecutionStatus.Running));
                Assert.That(scope.GetGroup(rule.Id).State.TotalFinished, Is.Zero);
                gate.SetResult(true);
                await ExecutionTestSupport.WaitUntil(() => scope.GetGroup(rule.Id).Executions.Count == 0);
                Assert.That(scope.GetGroup(rule.Id).State.TotalFinished, Is.EqualTo(1));
                scope.Fire(_event, -1, conditionContext, actionContext);
                Assert.That(seen.Count, Is.EqualTo(1));
            }
            finally { gate.TrySetResult(true); }
        }

        [Test]
        public void DelegateOptionalConditionPreservesNullContextsAndRequiresTaskDelegate()
        {
            _runtime.ConnectSystem(_system);
            var calls = 0;
            var rule = _runtime.CreateRule<int>("r", _event.Id, (state, context, commands) =>
            { Assert.That(context, Is.Null); calls++; return Task.CompletedTask; });
            Assert.That(rule.Condition, Is.Null);
            var scope = _runtime.CreateScope();
            scope.Register(rule, Allow);
            scope.Fire(_event, 1);
            Assert.That(calls, Is.EqualTo(1));
            Assert.Throws<ArgumentNullException>(() => _runtime.CreateRule<int>("bad", _event.Id, null));
        }

        [Test]
        public void AllFacadeOverloadsRejectDisposedRuntimeFirst()
        {
            _runtime.Dispose();
            Assert.Throws<ObjectDisposedException>(() => _runtime.CreateRule<int, RecordAction>(null, null));
            Assert.Throws<ObjectDisposedException>(() => _runtime.CreateRule<int, Positive, RecordAction>(null, null));
            Assert.Throws<ObjectDisposedException>(() => _runtime.CreateRule<int, CustomState, CustomCondition, CustomAction>(null, null));
            Assert.Throws<ObjectDisposedException>(() => _runtime.CreateRule<int>(null, null, null));
        }

        [Test]
        public void ScopeRuntimeUsesInjectedServicesForRootsAndChildren()
        {
            _runtime.ConnectSystem(_system);
            var rule = _runtime.CreateRule<int, Positive, RecordAction>("r", _event.Id);
            var checker = new CountingChecker();
            var runner = new CountingRunner();
            using var scopes = new EcaScopeRuntime(_system.Events, checker, runner);
            var root = scopes.CreateScope();
            var child = root.CreateScope();
            foreach (var scope in new[] { root, child }) { scope.Register(rule, Allow); scope.Fire(_event, 3); }
            Assert.That(checker.Calls, Is.EqualTo(2));
            Assert.That(runner.Calls, Is.EqualTo(2));
            Assert.That(root.GetGroup("r"), Is.Not.SameAs(child.GetGroup("r")));
            Assert.Throws<ArgumentNullException>(() => new EcaScopeRuntime(_system.Events, null, runner));
            Assert.Throws<ArgumentNullException>(() => new EcaScopeRuntime(_system.Events, checker, null));
        }

        public sealed class Positive : AEcaCondition<EcaScopeRuleState<int>>
        {
            public override string Id => "positive";
            public override bool Check(EcaScopeRuleState<int> state, IEcaConditionContext context) => state.EventState > 0;
        }
        public sealed class RecordAction : AEcaAction<EcaScopeRuleState<int>>
        {
            public override string Id => "record";
            public IEcaCommands Capability => Commands;
            public readonly List<EcaScopeRuleState<int>> Seen = new();
            public override Task Run(EcaScopeRuleState<int> state, IEcaActionContext context) { Seen.Add(state); return Task.CompletedTask; }
        }
        public sealed class CommandAction : AEcaAction<EcaScopeRuleState<int>>
        {
            public override string Id => "command-action";
            public EcaScopeRuleState<int> Seen;
            public override Task Run(EcaScopeRuleState<int> state, IEcaActionContext context)
            { Seen = state; return Commands.Run("capture", state, context, 1); }
        }
        public sealed class CustomState : IEcaScopeRuleState<int>
        {
            public int EventState { get; set; }
            public EcaExecutionGroupState ExecutionGroupState { get; set; }
            public EcaScopeState ScopeState { get; set; }
        }
        public sealed class CustomCondition : AEcaCondition<CustomState>
        {
            public override string Id => "custom-condition";
            public CustomState Seen;
            public override bool Check(CustomState state, IEcaConditionContext context) { Seen = state; return true; }
        }
        public sealed class CustomAction : AEcaAction<CustomState>
        {
            public override string Id => "custom-action";
            public CustomState Seen;
            public override Task Run(CustomState state, IEcaActionContext context) { Seen = state; return Task.CompletedTask; }
        }
        private sealed class CaptureCommand : AEcaCommand<IEcaRuleState, IEcaActionContext, int>
        {
            public override string Id => "capture";
            public Func<IEcaRuleState, IEcaActionContext, int, Task> Handler;
            public override Task Run(IEcaRuleState state, IEcaActionContext context, int args) => Handler(state, context, args);
        }
        private sealed class LyingEvent : IEcaEvent<int>
        {
            public string Id => "liar";
            public string Name => Id;
            public string Description => null;
            public Type EventStateType => typeof(string);
        }
        private sealed class CountingChecker : IEcaConditionChecker
        {
            public int Calls;
            public bool Check<R>(IEcaCondition<R> condition, R state, IEcaConditionContext context) where R : IEcaRuleState
            { Calls++; return condition.Check(state, context); }
        }
        private sealed class CountingRunner : IEcaActionRunner
        {
            public int Calls;
            public Task Run<R>(IEcaAction<R> action, R state, IEcaActionContext context) where R : IEcaRuleState
            { Calls++; return action.Run(state, context); }
        }
    }
}
