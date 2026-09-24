using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EcaSystems.Core2;
using EcaSystems.Variables;
using EcaSystems.Variables.Eca;
using NUnit.Framework;

namespace EcaSystems.Tests.VariablesEca
{
    public sealed class VariablesEcaAdapterTests
    {
        private const string Changed = "variables.variable.changed";
        private const string Set = "variables.variable.set";
        private const string Force = "variables.variable.force-set";
        private EcaVariablesSystem _variables;
        private EcaSystem _system;
        private EcaBaseEventRegistry _events;
        private EcaCommandRegistry _commands;
        private EcaStateRegistry _states;
        private EcaSystemRegistry _systems;
        private EcaSystemNamespaceRegistry _namespaces;
        private EcaSystemConnector _connector;
        private VariablesEcaAdapter _adapter;
        private RecordingEmitter _emitter;
        private readonly RuleState _ruleState = new();

        [SetUp]
        public void SetUp()
        {
            _variables = new EcaVariablesSystem();
            _system = VariablesEcaSetup.CreateSystem(_variables);
            _events = new EcaBaseEventRegistry();
            _commands = new EcaCommandRegistry();
            _states = new EcaStateRegistry();
            _systems = new EcaSystemRegistry();
            _namespaces = new EcaSystemNamespaceRegistry();
            _connector = new EcaSystemConnector(_systems, _namespaces, _events, _commands, _states);
            _emitter = new RecordingEmitter(_events);
            _adapter = new VariablesEcaAdapter(_variables, _system.Events, _emitter);
        }

        [TearDown] public void TearDown() => _adapter.Disconnect();
        private void Connect() { _connector.Connect(_system); _adapter.Connect(); }
        private Task Run(string id, EcaSetVariableArgs args) => new EcaCommandRunner(_commands).Run(id, _ruleState, null, args);

        [Test]
        public void DescriptorAndConnectorExposeExactCanonicalExportsAndDisconnectAll()
        {
            Assert.That(_system.Id, Is.EqualTo("variables"));
            Assert.That(_system.Namespace.Id, Is.EqualTo("variables"));
            Assert.That(_system.Name, Is.EqualTo("Variables"));
            Assert.That(_system.Events.Events.Select(e => e.Id), Is.EquivalentTo(new[] { Changed }));
            var declaration = _system.Events.Resolve(Changed);
            Assert.That(declaration, Is.InstanceOf<IEcaEvent<EcaVariableChangedEventState>>());
            Assert.That(declaration.EventStateType, Is.EqualTo(typeof(EcaVariableChangedEventState)));
            Assert.That(_system.Commands.Commands.Select(c => c.Id), Is.EquivalentTo(new[] { Set, Force }));
            foreach (var command in _system.Commands.Commands)
            {
                Assert.That(command.RuleStateType, Is.EqualTo(typeof(IEcaRuleState)));
                Assert.That(command.ContextType, Is.EqualTo(typeof(IEcaActionContext)));
                Assert.That(command.ArgsType, Is.EqualTo(typeof(EcaSetVariableArgs)));
            }
            var stateIds = Enum.GetValues(typeof(EcaVariablesStateKey)).Cast<EcaVariablesStateKey>().Select(EcaVariablesStateIds.Get).ToArray();
            Assert.That(stateIds, Is.EquivalentTo(new[] { "variables.state", "variables.store", "variables.subtree" }));
            Assert.That(Enum.GetValues(typeof(EcaVariablesEventKey)).Cast<EcaVariablesEventKey>().Select(EcaVariablesEventIds.Get), Is.EquivalentTo(new[] { Changed }));
            Assert.That(Enum.GetValues(typeof(EcaVariablesCommandKey)).Cast<EcaVariablesCommandKey>().Select(EcaVariablesCommandIds.Get), Is.EquivalentTo(new[] { Set, Force }));
            _connector.Connect(_system);
            Assert.That(_systems.Resolve("variables"), Is.SameAs(_system));
            Assert.That(_namespaces.Resolve("variables"), Is.SameAs(_system.Namespace));
            Assert.That(_events.Resolve(Changed), Is.SameAs(declaration));
            foreach (var command in _system.Commands.Commands)
                Assert.That(_commands.Resolve(command.Id), Is.SameAs(command));
            foreach (var id in stateIds) Assert.That(_states.Contains(id), Is.True);
            _connector.Disconnect(_system);
            Assert.That(_systems.Contains("variables"), Is.False);
            Assert.That(_namespaces.Contains("variables"), Is.False);
            Assert.That(_events.Events, Is.Empty);
            Assert.That(_commands.Commands, Is.Empty);
            foreach (var id in stateIds)
            {
                Assert.That(_states.Contains(id), Is.False);
                Assert.That(_system.States.Contains(id), Is.True);
            }
            _connector.Connect(_system);
            Assert.That(_events.Resolve(Changed), Is.SameAs(declaration));
            Assert.That(new EcaStateResolver(_states).Resolve<EcaVariablesSystemState>("variables.state", _ruleState).Stores, Is.Empty);
        }

        [Test]
        public void StateResolversUseExplicitSelectionAndReturnPointInTimeCoreSnapshots()
        {
            _connector.Connect(_system);
            var root = _variables.CreateStore("root");
            var child = _variables.CreateStore("child", "root");
            _variables.CreateStore("other");
            root.Declare("parent", 1);
            var variable = child.Declare("local", 2);
            var resolver = new EcaStateResolver(_states);
            var whole = resolver.Resolve<EcaVariablesSystemState>("variables.state", _ruleState);
            var store = resolver.Resolve<EcaVariableStoreState>("variables.store", _ruleState, new EcaVariablesStatePayload("child"));
            var subtree = resolver.Resolve<EcaVariableSubtreeState>("variables.subtree", _ruleState, new EcaVariablesStatePayload("root"));
            Assert.That(whole.Stores.Select(s => s.StoreId), Is.EquivalentTo(new[] { "root", "child", "other" }));
            Assert.That(store.StoreId, Is.EqualTo("child"));
            Assert.That(store.Variables.Select(v => v.Definition.Id), Is.EquivalentTo(new[] { "local" }));
            Assert.That(subtree.RootStoreId, Is.EqualTo("root"));
            Assert.That(subtree.Stores.Select(s => s.StoreId), Is.EquivalentTo(new[] { "root", "child" }));
            variable.SetValue(3);
            child.Declare("later", false);
            _variables.CreateStore("later", "root");
            Assert.That(store.Variables.Single().CurrentValue, Is.EqualTo(2));
            Assert.That(subtree.Stores, Has.Count.EqualTo(2));
            Assert.That(subtree.Stores.Single(s => s.StoreId == "child").Variables.Single().CurrentValue, Is.EqualTo(2));
            Assert.That(whole.Stores, Has.Count.EqualTo(3));
            Assert.That(whole.Stores.Single(s => s.StoreId == "child").Variables.Single().CurrentValue, Is.EqualTo(2));
            Assert.That(resolver.Resolve<EcaVariablesSystemState>("variables.state", _ruleState).Stores, Has.Count.EqualTo(4));
            Assert.That(resolver.Resolve<EcaVariableStoreState>("variables.store", _ruleState, new EcaVariablesStatePayload("child")).Variables, Has.Count.EqualTo(2));
        }

        [Test]
        public void ResolverShapesAndPayloadTypesNeverFallback()
        {
            _connector.Connect(_system);
            var resolver = new EcaStateResolver(_states);
            Assert.Throws<InvalidOperationException>(() => resolver.Resolve<EcaVariablesSystemState>("variables.state", _ruleState, null));
            Assert.Throws<InvalidOperationException>(() => resolver.Resolve<EcaVariableStoreState>("variables.store", _ruleState));
            Assert.Throws<InvalidOperationException>(() => resolver.Resolve<EcaVariableSubtreeState>("variables.subtree", _ruleState));
            foreach (var payload in new object[] { null, "root", new object() })
            {
                Assert.Throws<ArgumentException>(() => resolver.Resolve<EcaVariableStoreState>("variables.store", _ruleState, payload));
                Assert.Throws<ArgumentException>(() => resolver.Resolve<EcaVariableSubtreeState>("variables.subtree", _ruleState, payload));
            }
            var missing = new EcaVariablesStatePayload("missing");
            Assert.Throws<InvalidOperationException>(() => resolver.Resolve<EcaVariableStoreState>("variables.store", _ruleState, missing));
            Assert.Throws<InvalidOperationException>(() => resolver.Resolve<EcaVariableSubtreeState>("variables.subtree", _ruleState, missing));
            Assert.Throws<InvalidOperationException>(() => resolver.Resolve<object>("variables.state", _ruleState));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void PayloadAndArgsRejectInvalidIds(string id)
        {
            Assert.Throws<ArgumentException>(() => new EcaVariablesStatePayload(id));
            Assert.Throws<ArgumentException>(() => new EcaSetVariableArgs(id, "variable", null));
            Assert.Throws<ArgumentException>(() => new EcaSetVariableArgs("store", id, null));
            Assert.That(new EcaSetVariableArgs("store", "variable", null).Value, Is.Null);
        }

        [Test] public void IntCommands() => VerifyCommands(1, 2);
        [Test] public void FloatCommands() => VerifyCommands(1f, 2.5f);
        [Test] public void BoolCommands() => VerifyCommands(false, true);
        [Test] public void StringCommands() => VerifyCommands("one", "two");
        [Test] public void NullStringCommands() => VerifyCommands<string>("one", null);

        private void VerifyCommands<T>(T initial, T changed)
        {
            Connect();
            var parent = _variables.CreateStore("parent");
            var store = _variables.CreateStore("selected-store", "parent");
            var variable = store.Declare("v", initial);
            var order = new List<string>();
            store.VariableChanged += _ => order.Add("store");
            parent.VariableChanged += _ => order.Add("parent");
            _emitter.OnFire = _ => order.Add("eca");
            Assert.That(Run(Set, new EcaSetVariableArgs(store.Id, "v", changed)).IsCompletedSuccessfully, Is.True);
            Assert.That(order, Is.EqualTo(new[] { "store", "parent", "eca" }));
            Assert.That(variable.GetValue<T>(), Is.EqualTo(changed));
            var first = _emitter.Records.Single();
            Assert.That(first.StoreId, Is.EqualTo(store.Id));
            Assert.That(first.VariableId, Is.EqualTo("v"));
            Assert.That(first.ValueType, Is.EqualTo(typeof(T)));
            Assert.That(first.OldValue, Is.EqualTo(initial));
            Assert.That(first.CurrentValue, Is.EqualTo(changed));
            Run(Set, new EcaSetVariableArgs(store.Id, "v", changed));
            Assert.That(_emitter.Records, Has.Count.EqualTo(1));
            Assert.That(variable.OldValue, Is.EqualTo(initial));
            Run(Force, new EcaSetVariableArgs(store.Id, "v", changed));
            Assert.That(_emitter.Records, Has.Count.EqualTo(2));
            Assert.That(_emitter.Records[1].OldValue, Is.EqualTo(changed));
            Assert.That(_emitter.Records[1].CurrentValue, Is.EqualTo(changed));
            Run(Force, new EcaSetVariableArgs(store.Id, "v", initial));
            Assert.That(_emitter.Records, Has.Count.EqualTo(3));
            Assert.That(_emitter.Records[2].OldValue, Is.EqualTo(changed));
            Assert.That(_emitter.Records[2].CurrentValue, Is.EqualTo(initial));
            Assert.That(first.CurrentValue, Is.EqualTo(changed));
            Assert.That(variable.Definition.DefaultValue, Is.EqualTo(initial));
        }

        [TestCase(Set)]
        [TestCase(Force)]
        public void CommandValueValidationIsExactAndNeverMutatesOnMismatch(string command)
        {
            Connect();
            var store = _variables.CreateStore("store");
            store.Declare("int", 1); store.Declare("float", 1f);
            store.Declare("bool", false); store.Declare("string", "one");
            foreach (var variable in store.Variables.Variables)
            {
                foreach (var wrong in new object[] { 2, 2f, true, "two", 2d, 2L, 2m, new object(), DayOfWeek.Monday, null })
                {
                    var type = variable.Definition.ValueType;
                    if ((wrong == null && type == typeof(string)) || (wrong != null && wrong.GetType() == type)) continue;
                    Assert.Throws<ArgumentException>(() => Run(command, new EcaSetVariableArgs(store.Id, variable.Definition.Id, wrong)));
                    Assert.That(variable.CurrentValue, Is.EqualTo(variable.Definition.DefaultValue));
                    Assert.That(variable.OldValue, Is.EqualTo(variable.Definition.DefaultValue));
                }
            }
            Assert.That(_emitter.Records, Is.Empty);
        }

        [TestCase(Set)]
        [TestCase(Force)]
        public void CommandsRejectNullArgsAndMissingStoreOrLocalVariable(string command)
        {
            Connect();
            _variables.CreateStore("parent").Declare("v", 1);
            _variables.CreateStore("child", "parent");
            Assert.Throws<ArgumentNullException>(() => Run(command, null));
            Assert.Throws<InvalidOperationException>(() => Run(command, new EcaSetVariableArgs("missing", "v", 2)));
            Assert.Throws<InvalidOperationException>(() => Run(command, new EcaSetVariableArgs("child", "v", 2)));
            Assert.That(_variables.GetStore("parent").GetValue<int>("v"), Is.EqualTo(1));
            Assert.That(_emitter.Records, Is.Empty);
        }

        [Test]
        public void AdapterLifecycleIsExplicitIdempotentDisconnectAndSupportsFutureStores()
        {
            _connector.Connect(_system);
            _adapter.Disconnect();
            var early = _variables.CreateStore("early").Declare("v", 0);
            early.SetValue(1);
            Assert.That(_emitter.Records, Is.Empty);
            _adapter.Connect();
            Assert.Throws<InvalidOperationException>(() => _adapter.Connect());
            var late = _variables.CreateStore("late").Declare("v", 0);
            late.SetValue(1);
            Assert.That(_emitter.Records.Single().StoreId, Is.EqualTo("late"));
            _adapter.Disconnect(); _adapter.Disconnect();
            _connector.Disconnect(_system);
            early.SetValue(2); late.SetValue(2);
            Assert.That(_emitter.Records, Has.Count.EqualTo(1));
            Connect(); early.SetValue(3);
            Assert.That(_emitter.Records, Has.Count.EqualTo(2));
            Assert.That(_emitter.Records[1].StoreId, Is.EqualTo("early"));
        }

        [Test]
        public void AdapterCachesCanonicalDeclarationOnce()
        {
            var registry = new CountingRegistry(_system.Events);
            _adapter = new VariablesEcaAdapter(_variables, registry, _emitter);
            Assert.That(registry.ResolveCount, Is.EqualTo(1));
            Connect();
            var variable = _variables.CreateStore("store").Declare("v", 0);
            variable.SetValue(1); variable.ForceSetValue(1);
            Assert.That(registry.ResolveCount, Is.EqualTo(1));
            Assert.That(_emitter.Declarations, Has.Count.EqualTo(2));
            foreach (var declaration in _emitter.Declarations)
                Assert.That(declaration, Is.SameAs(_system.Events.Resolve(Changed)));
        }

        [Test]
        public void EventIsSeparateFlatSnapshotAndSurvivesReentrantMutation()
        {
            EcaVariableChanged core = null;
            _variables.VariableChanged += change => core = change;
            Connect();
            var variable = _variables.CreateStore("store").Declare("v", 0);
            _emitter.OnFire = state =>
            {
                Assert.That((object)state, Is.Not.SameAs(core));
                if ((int)state.CurrentValue == 1) variable.SetValue(2);
            };
            variable.SetValue(1);
            Assert.That(_emitter.Records.Select(s => s.CurrentValue), Is.EqualTo(new object[] { 1, 2 }));
            Assert.That(_emitter.Records[0].OldValue, Is.EqualTo(0));
            Assert.That(_emitter.Records[1].OldValue, Is.EqualTo(1));
            Assert.That(_emitter.Records[0], Is.Not.SameAs(_emitter.Records[1]));
            Assert.That(variable.CurrentValue, Is.EqualTo(2));
        }

        [Test]
        public void EarlierCoreExceptionStopsEcaEventWithoutRollback()
        {
            Connect();
            var store = _variables.CreateStore("store");
            var variable = store.Declare("v", 0);
            var failure = new Exception("core subscriber");
            store.VariableChanged += _ => throw failure;
            Assert.That(Assert.Throws<Exception>(() => Run(Set, new EcaSetVariableArgs("store", "v", 1))), Is.SameAs(failure));
            Assert.That(variable.CurrentValue, Is.EqualTo(1));
            Assert.That(_emitter.Records, Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ConstructorRejectsWrongEventInterfaceOrMetadata(bool wrongMetadata)
        {
            _system.Events.Unregister(Changed);
            _system.Events.Register(wrongMetadata
                ? (IEcaEvent)new Declaration<EcaVariableChangedEventState>(typeof(int))
                : new Declaration<int>(typeof(EcaVariableChangedEventState)));
            Assert.Throws<ArgumentException>(() => new VariablesEcaAdapter(_variables, _system.Events, _emitter));
        }

        [Test]
        public void ConstructorsAndMapperRejectNullAndMissingDeclaration()
        {
            Assert.Throws<ArgumentNullException>(() => VariablesEcaSetup.CreateSystem(null));
            Assert.Throws<ArgumentNullException>(() => new EcaSetVariableCommand(null));
            Assert.Throws<ArgumentNullException>(() => new EcaForceSetVariableCommand(null));
            Assert.Throws<ArgumentNullException>(() => new VariablesEcaAdapter(null, _system.Events, _emitter));
            Assert.Throws<ArgumentNullException>(() => new VariablesEcaAdapter(_variables, null, _emitter));
            Assert.Throws<ArgumentNullException>(() => new VariablesEcaAdapter(_variables, _system.Events, null));
            Assert.Throws<ArgumentNullException>(() => new EcaVariableChangedEventStateMapper().Map(null));
            _system.Events.Unregister(Changed);
            Assert.Throws<InvalidOperationException>(() => new VariablesEcaAdapter(_variables, _system.Events, _emitter));
            Assert.Throws<ArgumentOutOfRangeException>(() => EcaVariablesEventIds.Get((EcaVariablesEventKey)(-1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => EcaVariablesCommandIds.Get((EcaVariablesCommandKey)(-1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => EcaVariablesStateIds.Get((EcaVariablesStateKey)(-1)));
        }

        [Test]
        public void RealScopeEmitterReceivesNullContextsLocallyWithUnrelatedStoreId()
        {
            using var runtime = new EcaSystemsRuntime();
            runtime.ConnectSystem(_system);
            var scope = runtime.CreateScope("unrelated-scope");
            var other = runtime.CreateScope("other");
            var states = new List<EcaVariableChangedEventState>();
            var conditionCalls = 0;
            var otherCalls = 0;
            var rule = runtime.CreateRule<EcaVariableChangedEventState>("unrelated-rule", Changed,
                (state, context, commands) => { Assert.That(context, Is.Null); states.Add(state.EventState); return Task.CompletedTask; },
                (state, context) => { Assert.That(context, Is.Null); conditionCalls++; return true; });
            scope.Register(rule, new EcaExecutionMode(EcaExecutionModeOverlap.Allow));
            other.Register(runtime.CreateRule<EcaVariableChangedEventState>("other-rule", Changed,
                (state, context, commands) => { otherCalls++; return Task.CompletedTask; }), new EcaExecutionMode(EcaExecutionModeOverlap.Allow));
            _adapter = new VariablesEcaAdapter(_variables, _system.Events, scope.EventEmitter);
            _adapter.Connect();
            _variables.CreateStore("explicit-store").Declare("v", 0).SetValue(1);
            Assert.That(states.Single().StoreId, Is.EqualTo("explicit-store"));
            Assert.That(conditionCalls, Is.EqualTo(1));
            Assert.That(otherCalls, Is.Zero);
            Assert.That(scope.GetGroup("unrelated-rule").State.TotalFinished, Is.EqualTo(1));
            _adapter.Disconnect();
        }

        private sealed class RuleState : IEcaRuleState { public string RuleId => "not-a-store"; }
        private sealed class Declaration<E> : IEcaEvent<E>
        {
            public string Id => Changed;
            public string Name => Id;
            public string Description => Id;
            public Type EventStateType { get; }
            internal Declaration(Type type) => EventStateType = type;
        }
        private sealed class CountingRegistry : IEcaEventRegistry
        {
            private readonly IEcaEventRegistry _inner;
            internal int ResolveCount;
            internal CountingRegistry(IEcaEventRegistry inner) => _inner = inner;
            public IReadOnlyCollection<IEcaEvent> Events => _inner.Events;
            public void Register(IEcaEvent item) => _inner.Register(item);
            public bool Unregister(string id) => _inner.Unregister(id);
            public bool Contains(string id) => _inner.Contains(id);
            public bool CheckRegistered(IEcaEvent item) => _inner.CheckRegistered(item);
            public IEcaEvent Resolve(string id) { ResolveCount++; return _inner.Resolve(id); }
        }
        private sealed class RecordingEmitter : IEcaEventEmitter
        {
            private readonly IEcaEventRegistry _events;
            internal readonly List<EcaVariableChangedEventState> Records = new();
            internal readonly List<IEcaEvent> Declarations = new();
            internal Action<EcaVariableChangedEventState> OnFire;
            internal RecordingEmitter(IEcaEventRegistry events) => _events = events;
            public void Fire<E>(IEcaEvent<E> ecaEvent, E eventState, IEcaConditionContext conditionContext = null, IEcaActionContext actionContext = null)
            {
                Assert.That(conditionContext, Is.Null); Assert.That(actionContext, Is.Null);
                Assert.That(_events.CheckRegistered(ecaEvent), Is.True);
                Assert.That(typeof(E), Is.EqualTo(typeof(EcaVariableChangedEventState)));
                var state = (EcaVariableChangedEventState)(object)eventState;
                Records.Add(state); Declarations.Add(ecaEvent); OnFire?.Invoke(state);
            }
        }
    }
}
