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
    public sealed class VariablesLifecycleTests
    {
        private EcaVariablesSystem _variables;
        private EcaSystem _system;
        private EcaSystemsRuntime _runtime;
        private const string Changed = "variables.variable.changed";
        private const string SetRule = "variables.rule.variable.set";
        private EcaExecutionMode Mode => new(EcaExecutionModeOverlap.Allow);
        [SetUp]
        public void Setup()
        {
            _variables = new EcaVariablesSystem(); _system = VariablesEcaSetup.CreateSystem(_variables);
            _runtime = new EcaSystemsRuntime(); _runtime.ConnectSystem(_system);
        }
        [TearDown] public void Cleanup() => _runtime.Dispose();
        private EcaRule<EcaVariableChangedEventState, EcaScopeRuleState<EcaVariableChangedEventState>> Rule(string id,
            System.Action<EcaScopeRuleState<EcaVariableChangedEventState>> observe = null) =>
            _runtime.CreateRule<EcaVariableChangedEventState>(id, Changed, (state, context, commands) =>
            { Assert.That(context, Is.Null); observe?.Invoke(state); return Task.CompletedTask; });

        [Test]
        public void ScopeAndRuleStoresArePersistentAndReconnectExactInstances()
        {
            var root = _runtime.CreateScope("gameplay"); var child = root.CreateScope("combat");
            var scopeStore = _variables.GetStore("scope:gameplay/combat");
            Assert.That(scopeStore.ParentId, Is.EqualTo("scope:gameplay"));
            var rule = Rule("init");
            Assert.That(_variables.ContainsStore("rule:gameplay/combat:init"), Is.False, "CreateRule has no membership lifecycle.");
            child.Register(rule, Mode);
            var ruleStore = _variables.GetStore("rule:gameplay/combat:init");
            Assert.That(ruleStore.ParentId, Is.EqualTo(scopeStore.Id));
            ruleStore.Declare("health", 100).SetValue(42);
            child.Unregister(rule);
            Assert.That(_variables.GetStore(ruleStore.Id), Is.SameAs(ruleStore));
            child.Register(rule, Mode);
            Assert.That(_variables.GetStore(ruleStore.Id).GetValue<int>("health"), Is.EqualTo(42));
            child.Dispose();
            Assert.That(_variables.GetStore(scopeStore.Id), Is.SameAs(scopeStore));
            var replacement = root.CreateScope("combat"); replacement.Register(rule, Mode);
            Assert.That(replacement, Is.Not.SameAs(child));
            Assert.That(_variables.GetStore(ruleStore.Id), Is.SameAs(ruleStore));
            Assert.That(ruleStore.GetValue<int>("health"), Is.EqualTo(42));
        }

        [Test]
        public void ReusedScopeIdUnderDifferentParentsHasDifferentPersistentPath()
        {
            var gameplay = _runtime.CreateScope("gameplay"); var menu = _runtime.CreateScope("menu");
            var combat = gameplay.CreateScope("combat");
            _variables.GetStore("scope:gameplay/combat").Declare("v", 1);
            Assert.Throws<InvalidOperationException>(() => menu.CreateScope("combat"));
            combat.Dispose(); menu.CreateScope("combat");
            var other = _variables.GetStore("scope:menu/combat");
            Assert.That(other.ParentId, Is.EqualTo("scope:menu")); Assert.That(other.Contains("v"), Is.False);
            Assert.That(_variables.GetStore("scope:gameplay/combat").GetValue<int>("v"), Is.EqualTo(1));
        }

        [Test]
        public void PathSegmentsEscapeSeparatorsWithoutCollisions()
        {
            var flat = _runtime.CreateScope("a/b"); var a = _runtime.CreateScope("a"); a.CreateScope("b");
            flat.Register(Rule("r:x%"), Mode);
            Assert.That(_variables.ContainsStore("scope:a%2Fb"), Is.True);
            Assert.That(_variables.ContainsStore("scope:a/b"), Is.True);
            Assert.That(_variables.GetStore("rule:a%2Fb:r%3Ax%25").ParentId, Is.EqualTo("scope:a%2Fb"));
        }

        [Test]
        public void OrdinaryEventsRouteOnlySelfAndAncestorsWhileAggregateRemainsOnce()
        {
            var root = _runtime.CreateScope("root"); var child = root.CreateScope("child");
            var leaf = child.CreateScope("leaf"); var sibling = root.CreateScope("sibling"); var other = _runtime.CreateScope("other");
            var calls = new List<string>(); var sources = new List<string>();
            foreach (var scope in new[] { root, child, leaf, sibling, other })
                scope.Register(Rule("observe-" + scope.ScopeId, state => { calls.Add(state.ScopeState.ScopeId); sources.Add(state.EventState.StoreId); }), Mode);
            var aggregate = 0; _variables.VariableChanged += _ => aggregate++;
            var variable = _variables.GetStore("scope:root/child").Declare("v", 0);
            variable.SetValue(1);
            Assert.That(calls, Is.EqualTo(new[] { "child", "root" }));
            Assert.That(sources, Is.EqualTo(new[] { "scope:root/child", "scope:root/child" }));
            Assert.That(aggregate, Is.EqualTo(1));
            calls.Clear(); sources.Clear();
            _variables.GetStore("scope:root").Declare("v", 0).SetValue(1);
            Assert.That(calls, Is.EqualTo(new[] { "root" })); Assert.That(aggregate, Is.EqualTo(2));
        }

        [Test]
        public void LateSystemConnectSynchronizesExistingHierarchyAndRuleMembership()
        {
            _runtime.DisconnectSystem(_system);
            var events = new EcaBaseEventRegistry(); var declaration = new NumberEvent(); events.Register(declaration);
            _runtime.ConnectSystem(new EcaSystem("source", new EcaSystemNamespace("source"), events, new EcaCommandRegistry(), new EcaStateRegistry()));
            var root = _runtime.CreateScope("root"); var child = root.CreateScope("child");
            var rule = _runtime.CreateRule<int>("init", declaration.Id, (s, c, cmd) => Task.CompletedTask);
            root.Register(rule, Mode); child.Register(rule, Mode);
            Assert.That(_variables.GetState().Stores, Is.Empty);
            _runtime.ConnectSystem(_system);
            Assert.That(_variables.GetState().Stores.Select(s => s.StoreId), Is.EquivalentTo(new[] {
                "scope:root", "rule:root:init", "scope:root/child", "rule:root/child:init" }));
        }

        [Test]
        public void SystemDisconnectStopsBindingsAndReconnectPreservesValuesWithoutDuplicateSubscriptions()
        {
            var scope = _runtime.CreateScope("root"); var count = 0; scope.Register(Rule("r", _ => count++), Mode);
            var variable = _variables.GetStore("scope:root").Declare("v", 0);
            variable.SetValue(1); Assert.That(count, Is.EqualTo(1));
            _runtime.DisconnectSystem(_system); variable.SetValue(2); Assert.That(count, Is.EqualTo(1));
            _runtime.ConnectSystem(_system); variable.SetValue(3); Assert.That(count, Is.EqualTo(2));
            Assert.That(_variables.GetStore("scope:root").GetVariable("v"), Is.SameAs(variable));
        }

        [Test]
        public void FailedSubtreeDisconnectRestoresScopedSubscriptionsAndRuleAccess()
        {
            var fault = new FaultConnector();
            var faulty = new EcaSystem("zfail", new EcaSystemNamespace("zfail"), new EcaBaseEventRegistry(), new EcaCommandRegistry(), new EcaStateRegistry(), lifecycleConnector: fault);
            _runtime.ConnectSystem(faulty);
            var root = _runtime.CreateScope("root"); var child = root.CreateScope("child"); var seen = new List<string>();
            root.Register(Rule("r", s => seen.Add(s.ScopeState.ScopeId)), Mode);
            child.Register(Rule("r", s => seen.Add(s.ScopeState.ScopeId)), Mode);
            var variable = _variables.GetStore("rule:root/child:r").Declare("v", 0);
            fault.Fail = "root";
            Assert.Throws<InvalidOperationException>(() => root.Dispose());
            Assert.That(root.IsDisposed || child.IsDisposed, Is.False);
            variable.SetValue(1);
            Assert.That(seen, Is.EqualTo(new[] { "child", "root" }));
            fault.Fail = null; root.Dispose(); seen.Clear(); variable.SetValue(2);
            Assert.That(seen, Is.Empty);
        }

        [Test]
        public void FailedBindingWithConflictingPersistentParentDoesNotReserveScopeId()
        {
            _variables.CreateStore("foreign"); _variables.CreateStore("scope:root", "foreign");
            Assert.Throws<InvalidOperationException>(() => _runtime.CreateScope("root"));
            Assert.Throws<InvalidOperationException>(() => _runtime.CreateScope("root"), "Fails on Store conflict again, not an active Scope reservation.");
            _runtime.DisconnectSystem(_system);
            Assert.That(_runtime.CreateScope("root").IsDisposed, Is.False);
        }

        [Test] public void RuleCommandInt() => VerifyRuleCommand(1, 2);
        [Test] public void RuleCommandFloat() => VerifyRuleCommand(1f, 2f);
        [Test] public void RuleCommandBool() => VerifyRuleCommand(false, true);
        [Test] public void RuleCommandString() => VerifyRuleCommand("one", "two");
        [Test] public void RuleCommandNullString() => VerifyRuleCommand<string>("one", null);
        private void VerifyRuleCommand<T>(T initial, T changed)
        {
            var root = _runtime.CreateScope("root"); var child = root.CreateScope("child");
            var rule = Rule("r"); root.Register(rule, Mode); child.Register(rule, Mode);
            var first = _variables.GetStore("rule:root:r").Declare("v", initial);
            var second = _variables.GetStore("rule:root/child:r").Declare("v", initial);
            var state = new EcaScopeRuleState<int>("r", 0, child.GetGroup("r").State, child.State);
            var runner = new EcaCommandRunner(_system.Commands); var count = 0;
            _variables.VariableChanged += _ => count++;
            Assert.That(runner.Run(SetRule, state, null, new EcaSetRuleVariableArgs("v", changed)).IsCompletedSuccessfully, Is.True);
            Assert.That(second.GetValue<T>(), Is.EqualTo(changed)); Assert.That(second.OldValue, Is.EqualTo(initial));
            Assert.That(first.GetValue<T>(), Is.EqualTo(initial)); Assert.That(count, Is.EqualTo(1));
            runner.Run(SetRule, state, null, new EcaSetRuleVariableArgs("v", changed)); Assert.That(count, Is.EqualTo(1));
            Assert.Throws<ArgumentException>(() => runner.Run(SetRule, state, null, new EcaSetRuleVariableArgs("v", 1d)));
            if (typeof(T) != typeof(string)) Assert.Throws<ArgumentException>(() => runner.Run(SetRule, state, null, new EcaSetRuleVariableArgs("v", null)));
            Assert.Throws<ArgumentNullException>(() => runner.Run(SetRule, state, null, (EcaSetRuleVariableArgs)null));
            child.Unregister(rule);
            Assert.Throws<InvalidOperationException>(() => runner.Run(SetRule, state, null, new EcaSetRuleVariableArgs("v", initial)));
            Assert.That(second.GetValue<T>(), Is.EqualTo(changed));
            child.Register(rule, Mode); child.Dispose();
            var replacement = root.CreateScope("child"); replacement.Register(rule, Mode);
            Assert.Throws<InvalidOperationException>(() => runner.Run(SetRule, state, null, new EcaSetRuleVariableArgs("v", initial)), "Old ScopeState cannot address replacement binding.");
            Assert.That(_system.Commands.Contains("variables.rule.variable.force-set"), Is.False);
        }

        [Test]
        public void RuleSetIsImmediateReentrantAndSameValueTerminatesNaturally()
        {
            var scope = _runtime.CreateScope("root"); var calls = 0;
            var rule = _runtime.CreateRule<EcaVariableChangedEventState>("r", Changed, (state, context, commands) =>
            { calls++; return commands.Run(SetRule, state, context, new EcaSetRuleVariableArgs("v", 1)); });
            scope.Register(rule, Mode);
            var variable = _variables.GetStore("rule:root:r").Declare("v", 0);
            _variables.GetStore("scope:root").Declare("trigger", 0).SetValue(1);
            Assert.That(calls, Is.EqualTo(2)); Assert.That(variable.CurrentValue, Is.EqualTo(1));
            Assert.That(scope.GetGroup("r").State.TotalFinished, Is.EqualTo(2));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void RuleArgsRejectInvalidVariableId(string id) =>
            Assert.Throws<ArgumentException>(() => new EcaSetRuleVariableArgs(id, null));

        private sealed class NumberEvent : IEcaEvent<int>
        { public string Id => "source.number"; public string Name => Id; public string Description => Id; public Type EventStateType => typeof(int); }
        private sealed class FaultConnector : IEcaSystemLifecycleConnector
        {
            internal string Fail;
            public void ConnectScope(EcaScope scope) { }
            public void DisconnectScope(EcaScope scope) { if (scope.ScopeId == Fail) throw new InvalidOperationException("injected"); }
            public void ConnectRule(EcaScope scope, IEcaRule rule) { }
            public void DisconnectRule(EcaScope scope, IEcaRule rule) { }
        }
    }
}
