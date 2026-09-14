using System;
using System.Threading.Tasks;
using EcaSystems.Core2;
using NUnit.Framework;
using static EcaSystems.Tests.Core2.ExecutionTestSupport;

namespace EcaSystems.Tests.Core2
{
    [TestFixture]
    public sealed class EcaExecutionRegistryTests : ExecutionTestFixture
    {
        private EcaExecutionMode Mode => new(EcaExecutionModeOverlap.Allow);
        private static State CreateState(int value, EcaExecutionGroupState state) => new(value, state);

        [Test]
        public void Register_PopulatesSharedRuleRegistryAndTypedGroup()
        {
            var rule = NewRule();
            var mode = Mode;
            Runtime.Register(rule, mode, CreateState);
            var group = Runtime.GetGroup(rule.Id);
            Assert.That(group.Rule, Is.SameAs(rule));
            Assert.That(group.RuleId, Is.EqualTo(rule.Id));
            Assert.That(group.ExecutionMode, Is.SameAs(mode));
            Assert.That(group.State.TotalStarted, Is.Zero);
            Assert.That(group.State.TotalFinished, Is.Zero);
            Assert.That(Rules.GetByEvent<int, State, ConditionContext, ActionContext>(Event), Is.EqualTo(new[] { rule }));
            Assert.That(Groups.Get<int, State, ConditionContext, ActionContext>(rule.Id), Is.SameAs(group));
            Assert.That(Groups.TryGet<int, State, ConditionContext, ActionContext>(rule.Id, out var typed), Is.True);
            Assert.That(typed, Is.SameAs(group));
            Assert.That(Runtime.TryGetGroup(rule.Id, out var inspected), Is.True);
            Assert.That(inspected, Is.SameAs(group));
        }

        [Test]
        public void Registry_MissingAndIncompatibleTypesAreExplicit()
        {
            Runtime.Register(NewRule(), Mode, CreateState);
            Assert.Throws<InvalidOperationException>(() => Groups.Get("missing"));
            Assert.That(Runtime.TryGetGroup("missing", out var missing), Is.False);
            Assert.That(missing, Is.Null);
            Assert.That(Groups.TryGet<int, State, ConditionContext, ActionContext>("missing", out var typedMissing), Is.False);
            Assert.That(typedMissing, Is.Null);
            Assert.Throws<InvalidOperationException>(() => Groups.Get<int, State, ConditionContext, ActionContext>("missing"));
            Assert.Throws<InvalidOperationException>(() =>
                Groups.Get<int, EcaExecutionRuleState<int>, ConditionContext, ActionContext>("rule"));
            Assert.Throws<InvalidOperationException>(() =>
                Groups.Get<int, State, IEcaExecutionConditionContext, ActionContext>("rule"));
            Assert.Throws<InvalidOperationException>(() =>
                Groups.Get<int, State, ConditionContext, IEcaExecutionActionContext>("rule"));
            Assert.Throws<InvalidOperationException>(() =>
                Groups.Get<string, EcaExecutionRuleState<string>, ConditionContext, ActionContext>("rule"));
            Assert.That(Groups.TryGet<int, EcaExecutionRuleState<int>, ConditionContext, ActionContext>(
                "rule", out var incompatible), Is.False);
            Assert.That(incompatible, Is.Null);
            Assert.That(Groups.Unregister("missing"), Is.False);
            Assert.That(Groups.Unregister("rule"), Is.True);
            Assert.That(Groups.TryGet("rule", out _), Is.False);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" \t")]
        public void Registry_RejectsEmptyIds(string id)
        {
            var group = new EcaExecutionGroup<int, State, ConditionContext, ActionContext>(
                NewRule(id), Mode, CreateState, new EcaBaseConditionChecker(), new EcaBaseActionRunner());
            Assert.Throws<ArgumentException>(() => Groups.Register(group));
            Assert.Throws<ArgumentException>(() => Groups.Get(id));
            Assert.Throws<ArgumentException>(() => Groups.TryGet(id, out _));
            Assert.Throws<ArgumentException>(() => Groups.Unregister(id));
        }

        [Test]
        public void Register_DuplicatesLeaveOriginalGroupIntact()
        {
            var rule = NewRule();
            Runtime.Register(rule, Mode, CreateState);
            var original = Runtime.GetGroup(rule.Id);
            Assert.Throws<InvalidOperationException>(() => Runtime.Register(rule, Mode, CreateState));
            Assert.Throws<InvalidOperationException>(() => Runtime.Register(NewRule(), Mode, CreateState));
            Assert.Throws<InvalidOperationException>(() => Groups.Register(original));
            Assert.Throws<ArgumentNullException>(() => Groups.Register(null));
            Assert.That(Runtime.GetGroup(rule.Id), Is.SameAs(original));
            Assert.That(Rules.GetByEvent<int, State, ConditionContext, ActionContext>(Event), Is.EqualTo(new[] { rule }));
        }

        [Test]
        public void Register_RollsBackRuleWhenGroupRegistrationFails()
        {
            var original = new EcaExecutionGroup<int, State, ConditionContext, ActionContext>(
                NewRule(), Mode, CreateState, new EcaBaseConditionChecker(), new EcaBaseActionRunner());
            Groups.Register(original);
            Assert.Throws<InvalidOperationException>(() => Runtime.Register(NewRule(), Mode, CreateState));
            Assert.That(Rules.GetByEvent<int, State, ConditionContext, ActionContext>(Event), Is.Empty);
            Assert.That(Runtime.GetGroup("rule"), Is.SameAs(original));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Register_RollsBackRuleWhenGroupConstructionFails(bool missingMode)
        {
            var rule = NewRule();
            Assert.Throws<ArgumentNullException>(() => Runtime.Register(
                rule, missingMode ? null : Mode, missingMode ? CreateState : (Func<int, EcaExecutionGroupState, State>)null));
            Assert.That(Rules.GetByEvent<int, State, ConditionContext, ActionContext>(Event), Is.Empty);
            Assert.That(Runtime.TryGetGroup(rule.Id, out _), Is.False);
            Runtime.Register(rule, Mode, CreateState);
            Assert.That(Runtime.GetGroup(rule.Id).Rule, Is.SameAs(rule));
        }

        [Test]
        public void Register_InvalidRuleDoesNotCreateGroup()
        {
            var rule = NewRule();
            rule.Event = new BaseTestSupport.Event<int> { Id = "missing" };
            Assert.Throws<InvalidOperationException>(() => Runtime.Register(rule, Mode, CreateState));
            Assert.That(Runtime.TryGetGroup(rule.Id, out _), Is.False);
        }

        [Test]
        public void Unregister_RequiresOriginalInstanceAndRemovesFutureRouting()
        {
            var calls = 0;
            var rule = NewRule(run: (state, context) => { calls++; return Task.CompletedTask; });
            Runtime.Register(rule, Mode, CreateState);
            var group = Runtime.GetGroup(rule.Id);
            Assert.That(Runtime.Unregister(NewRule()), Is.False);
            Assert.That(Runtime.GetGroup(rule.Id), Is.SameAs(group));
            Fire();
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(Runtime.Unregister(rule), Is.True);
            Assert.That(Runtime.Unregister(rule), Is.False);
            Assert.That(Runtime.TryGetGroup(rule.Id, out _), Is.False);
            Fire();
            Assert.That(calls, Is.EqualTo(1));
            Assert.Throws<ArgumentNullException>(() => Runtime.Unregister(null));
        }

        [Test]
        public async Task Unregister_ActiveExecutionKeepsOldGroupAndReregistrationGetsNewLifetime()
        {
            var oldGate = NewGate();
            var newGate = NewGate();
            var calls = 0;
            var rule = NewRule(run: (state, context) => ++calls == 1 ? oldGate.Task : newGate.Task);
            var mode = new EcaExecutionMode(EcaExecutionModeOverlap.Ignore, 1);
            Runtime.Register(rule, mode, CreateState);
            Fire();
            var oldGroup = Runtime.GetGroup(rule.Id);
            var oldExecution = oldGroup.Executions[0];
            Assert.That(Runtime.Unregister(rule), Is.True);
            Assert.That(oldExecution.Status, Is.EqualTo(EcaExecutionStatus.Running));
            Runtime.Register(rule, mode, CreateState);
            var newGroup = Runtime.GetGroup(rule.Id);
            Assert.That(newGroup, Is.Not.SameAs(oldGroup));
            Assert.That(newGroup.State, Is.Not.SameAs(oldGroup.State));
            Assert.That(newGroup.State.TotalStarted, Is.Zero);
            Fire();
            Assert.That(calls, Is.EqualTo(2));
            Assert.That(newGroup.Executions, Has.Count.EqualTo(1));
            oldGate.Complete();
            await WaitUntil(() => oldGroup.Executions.Count == 0);
            Assert.That(oldExecution.Status, Is.EqualTo(EcaExecutionStatus.Completed));
            Assert.That(oldGroup.State.TotalFinished, Is.EqualTo(1));
            Assert.That(newGroup.State.TotalFinished, Is.Zero);
            Assert.That(newGroup.Executions, Has.Count.EqualTo(1));
            newGate.Complete();
            await WaitUntil(() => newGroup.Executions.Count == 0);
            Assert.That(newGroup.State.TotalFinished, Is.EqualTo(1));
        }

        [Test]
        public void Runtime_RejectsMissingDependencies()
        {
            var checker = new EcaBaseConditionChecker();
            var runner = new EcaBaseActionRunner();
            Assert.Throws<ArgumentNullException>(() => new EcaExecutionRuntime(null, Groups, checker, runner));
            Assert.Throws<ArgumentNullException>(() => new EcaExecutionRuntime(Rules, null, checker, runner));
            Assert.Throws<ArgumentNullException>(() => new EcaExecutionRuntime(Rules, Groups, null, runner));
            Assert.Throws<ArgumentNullException>(() => new EcaExecutionRuntime(Rules, Groups, checker, null));
        }
    }
}
