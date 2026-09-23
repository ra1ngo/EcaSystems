using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using EcaSystems.Core2;
using NUnit.Framework;
using static EcaSystems.Tests.Core2.ExecutionTestSupport;

namespace EcaSystems.Tests.Core2
{
    [TestFixture]
    public sealed class EcaExecutionGroupTests : ExecutionTestFixture
    {
        private EcaExecutionGroup<int, State> CreateGroup(
            EcaExecutionModeOverlap overlap = EcaExecutionModeOverlap.Allow, int limit = -1,
            Func<State, ConditionContext, bool> check = null, Func<State, ActionContext, Task> run = null,
            Func<int, EcaExecutionGroupState, State> createState = null)
        {
            var rule = NewRule(check: check, run: run);
            return new EcaExecutionGroup<int, State>(
                rule, new EcaExecutionMode(overlap, limit),
                createState ?? ((value, state) => new State(value, state) { RuleId = rule.Id }),
                new EcaBaseConditionChecker(), new EcaBaseActionRunner());
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Check_RejectsNullOrWrongRuleIdentityBeforeCondition(bool nullState)
        {
            var checks = 0;
            var group = CreateGroup(check: (state, context) => { checks++; return true; },
                createState: (value, state) => nullState ? null : new State(value, state) { RuleId = "foreign" });
            var error = Assert.Throws<InvalidOperationException>(() => group.Check(1, Conditions));
            Assert.That(error.Message, Does.Contain("RuleId"));
            Assert.That(checks, Is.Zero);
            Assert.That(group.State.TotalStarted, Is.Zero);
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task Run_RejectsNullOrWrongRuleIdentityAsFailedExecution(bool nullState)
        {
            var actions = 0;
            EcaExecution captured = null;
            EcaExecutionGroup<int, State> group = null;
            group = CreateGroup(run: (state, context) => { actions++; return Task.CompletedTask; },
                createState: (value, state) =>
                {
                    captured = group.Executions[0];
                    return nullState ? null : new State(value, state) { RuleId = "foreign" };
                });
            await group.Run(1, Actions);
            Assert.That(actions, Is.Zero);
            Assert.That(captured.Status, Is.EqualTo(EcaExecutionStatus.Failed));
            Assert.That(captured.Exception, Is.TypeOf<InvalidOperationException>());
            Assert.That(captured.Exception.Message, Does.Contain("RuleId"));
            Assert.That(group.State.TotalStarted, Is.EqualTo(1));
            Assert.That(group.State.TotalFinished, Is.EqualTo(1));
            Assert.That(group.Executions, Is.Empty);
        }

        [TestCase(EcaExecutionModeOverlap.Ignore, -1)]
        [TestCase(EcaExecutionModeOverlap.Allow, -1)]
        [TestCase(EcaExecutionModeOverlap.Allow, 0)]
        [TestCase(EcaExecutionModeOverlap.Allow, 3)]
        public void Mode_PreservesValidSettings(EcaExecutionModeOverlap overlap, int limit)
        {
            var mode = new EcaExecutionMode(overlap, limit);
            Assert.That(mode.Overlap, Is.EqualTo(overlap));
            Assert.That(mode.Limit, Is.EqualTo(limit));
            Assert.That(new EcaExecutionMode(overlap).Limit, Is.EqualTo(-1));
        }

        [TestCase(-2)]
        [TestCase(int.MinValue)]
        public void Mode_RejectsInvalidLimit(int limit)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EcaExecutionMode(EcaExecutionModeOverlap.Allow, limit));
        }

        [TestCase(-1)]
        [TestCase(2)]
        public void Mode_RejectsInvalidOverlap(int overlap)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EcaExecutionMode((EcaExecutionModeOverlap)overlap));
        }

        [Test]
        public void RuleState_PreservesPayloadAndLiveState()
        {
            var group = CreateGroup();
            var state = new EcaExecutionRuleState<int>("rule", 42, group.State);
            Assert.That(state.EventState, Is.EqualTo(42));
            Assert.That(state.ExecutionGroupState, Is.SameAs(group.State));
            Assert.Throws<ArgumentNullException>(() => new EcaExecutionRuleState<int>("rule", 42, null));
        }

        [Test]
        public void Execution_StartsPendingWithoutStateOrHistory()
        {
            var rule = NewRule();
            var execution = (EcaExecution)Activator.CreateInstance(typeof(EcaExecution),
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { 12L, rule }, null);
            Assert.That(execution.Id, Is.EqualTo(12));
            Assert.That(execution.Rule, Is.SameAs(rule));
            Assert.That(execution.RuleId, Is.EqualTo(rule.Id));
            Assert.That(execution.Status, Is.EqualTo(EcaExecutionStatus.Pending));
            Assert.That(execution.Exception, Is.Null);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Check_CreatesFreshStateWithoutAdmissionOrCounters(bool result)
        {
            var seen = new List<State>();
            ConditionContext received = null;
            var group = CreateGroup(limit: 0, check: (state, context) =>
            {
                seen.Add(state);
                received = context;
                return result;
            });
            Assert.That(group.Check(17, Conditions), Is.EqualTo(result));
            Assert.That(group.Check(18, Conditions), Is.EqualTo(result));
            Assert.That(seen, Has.Count.EqualTo(2));
            Assert.That(seen[0], Is.Not.SameAs(seen[1]));
            Assert.That(seen[0].EventState, Is.EqualTo(17));
            Assert.That(seen[1].EventState, Is.EqualTo(18));
            Assert.That(seen[0].ExecutionGroupState, Is.SameAs(group.State));
            Assert.That(received, Is.SameAs(Conditions));
            Assert.That(group.Executions, Is.Empty);
            Assert.That(group.State.TotalStarted, Is.Zero);
            Assert.That(group.State.TotalFinished, Is.Zero);
        }

        [Test]
        public void Check_NullConditionDoesNotCreateState()
        {
            var group = CreateGroup(limit: 0, createState: (value, state) => throw new Exception("Not needed"));
            Assert.That(group.Check(0, Conditions), Is.True);
            Assert.That(group.Executions, Is.Empty);
            Assert.That(group.State.TotalStarted, Is.Zero);
        }

        [Test]
        public async Task Run_TracksLifecycleAndSharesLiveGroupStateAcrossFreshStates()
        {
            var gate = NewGate();
            State checkedState = null, actionState = null;
            ActionContext received = null;
            EcaExecution observed = null;
            long startedInAction = -1, finishedInAction = -1;
            var group = CreateGroup(check: (state, context) => { checkedState = state; return true; });
            ((ExecutionTestSupport.Action<State, ActionContext>)((IEcaRule<int, State>)group.Rule).Action).Handler =
                (state, context) =>
                {
                    actionState = state;
                    received = context;
                    observed = group.Executions[0];
                    startedInAction = state.ExecutionGroupState.TotalStarted;
                    finishedInAction = state.ExecutionGroupState.TotalFinished;
                    return gate.Task;
                };
            Assert.That(group.Check(42, Conditions), Is.True);
            var task = group.Run(42, Actions);
            Assert.That(task.IsCompleted, Is.False);
            Assert.That(observed.Status, Is.EqualTo(EcaExecutionStatus.Running));
            Assert.That(observed.Rule, Is.SameAs(group.Rule));
            Assert.That(observed.Exception, Is.Null);
            Assert.That(actionState, Is.Not.SameAs(checkedState));
            Assert.That(actionState.EventState, Is.EqualTo(42));
            Assert.That(actionState.ExecutionGroupState, Is.SameAs(checkedState.ExecutionGroupState));
            Assert.That(received, Is.SameAs(Actions));
            Assert.That(startedInAction, Is.EqualTo(1));
            Assert.That(finishedInAction, Is.Zero);
            Assert.That(group.Executions, Has.Count.EqualTo(1));
            var view = (ICollection<EcaExecution>)group.Executions;
            Assert.That(view.IsReadOnly, Is.True);
            Assert.Throws<NotSupportedException>(() => view.Clear());

            gate.Complete();
            await Await(task);
            Assert.That(observed.Status, Is.EqualTo(EcaExecutionStatus.Completed));
            Assert.That(group.Executions, Is.Empty);
            Assert.That(checkedState.ExecutionGroupState.TotalFinished, Is.EqualTo(1));
        }

        [TestCase("throw")]
        [TestCase("fault")]
        [TestCase("null")]
        [TestCase("state")]
        public async Task Run_FailuresAreObservedAndCleanedUp(string failure)
        {
            var error = new InvalidOperationException("failure");
            EcaExecution execution = null;
            EcaExecutionGroup<int, State> group = null;
            group = CreateGroup(createState: (value, state) =>
            {
                execution = group.Executions[0];
                if (failure == "state") throw error;
                return new State(value, state);
            }, run: (state, context) =>
            {
                if (failure == "throw") throw error;
                return failure == "null" ? null : Task.FromException(error);
            });
            await Await(group.Run(1, Actions));
            Assert.That(execution.Status, Is.EqualTo(EcaExecutionStatus.Failed));
            if (failure == "null") Assert.That(execution.Exception, Is.TypeOf<InvalidOperationException>());
            else Assert.That(execution.Exception, Is.SameAs(error));
            Assert.That(group.State.TotalStarted, Is.EqualTo(1));
            Assert.That(group.State.TotalFinished, Is.EqualTo(1));
            Assert.That(group.Executions, Is.Empty);
        }

        [Test]
        public async Task Run_ObservesDelayedFailure()
        {
            var gate = NewGate();
            var error = new Exception("delayed");
            var group = CreateGroup(run: (state, context) => gate.Task);
            var task = group.Run(1, Actions);
            var execution = group.Executions[0];
            gate.Fail(error);
            await Await(task);
            Assert.That(execution.Status, Is.EqualTo(EcaExecutionStatus.Failed));
            Assert.That(execution.Exception, Is.SameAs(error));
            Assert.That(group.State.TotalFinished, Is.EqualTo(1));
            Assert.That(group.Executions, Is.Empty);
        }

        [TestCase(EcaExecutionModeOverlap.Allow, 2)]
        [TestCase(EcaExecutionModeOverlap.Ignore, 1)]
        public async Task Run_OverlapControlsActiveExecutions(EcaExecutionModeOverlap overlap, int count)
        {
            var gate = NewGate();
            var checks = 0;
            var group = CreateGroup(overlap, check: (state, context) => { checks++; return true; },
                run: (state, context) => gate.Task);
            var first = group.Run(1, Actions);
            var firstExecution = group.Executions[0];
            Assert.That(group.Check(2, Conditions), Is.True);
            Assert.That(checks, Is.EqualTo(1));
            var second = group.Run(2, Actions);
            Assert.That(group.Executions, Has.Count.EqualTo(count));
            Assert.That(group.State.TotalStarted, Is.EqualTo(count));
            Assert.That(group.State.TotalFinished, Is.Zero);
            if (count == 2) Assert.That(group.Executions[1].Id, Is.GreaterThan(firstExecution.Id));
            else Assert.That(second, Is.SameAs(Task.CompletedTask));
            gate.Complete();
            await Await(Task.WhenAll(first, second));
            await Await(group.Run(3, Actions));
            Assert.That(group.State.TotalStarted, Is.EqualTo(count + 1));
            Assert.That(group.State.TotalFinished, Is.EqualTo(count + 1));
            Assert.That(group.Executions, Is.Empty);
        }

        [TestCase(0, 0)]
        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(-1, 3)]
        public async Task Run_LimitIsLifetimeNotConcurrency(int limit, int expected)
        {
            var created = 0;
            var group = CreateGroup(limit: limit, createState: (value, state) => { created++; return new State(value, state); });
            for (var i = 0; i < 3; i++) await Await(group.Run(i, Actions));
            Assert.That(group.State.TotalStarted, Is.EqualTo(expected));
            Assert.That(group.State.TotalFinished, Is.EqualTo(expected));
            Assert.That(created, Is.EqualTo(expected));
        }

        [Test]
        public async Task Run_FailureConsumesLifetimeLimit()
        {
            var group = CreateGroup(limit: 1, run: (state, context) => throw new Exception("failed"));
            await Await(group.Run(1, Actions));
            await Await(group.Run(2, Actions));
            Assert.That(group.State.TotalStarted, Is.EqualTo(1));
            Assert.That(group.State.TotalFinished, Is.EqualTo(1));
        }

        [TestCase(EcaExecutionModeOverlap.Ignore, -1)]
        [TestCase(EcaExecutionModeOverlap.Allow, 1)]
        public async Task Run_AdmissionIsVisibleBeforeReentrantStateCreation(EcaExecutionModeOverlap overlap, int limit)
        {
            var calls = 0;
            EcaExecutionGroup<int, State> group = null;
            group = CreateGroup(overlap, limit, createState: (value, state) =>
            {
                calls++;
                if (calls == 1) _ = group.Run(2, Actions);
                return new State(value, state);
            });
            await Await(group.Run(1, Actions));
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(group.State.TotalStarted, Is.EqualTo(1));
            Assert.That(group.State.TotalFinished, Is.EqualTo(1));
        }

        [Test]
        public void Constructor_RejectsMissingDependencies()
        {
            var rule = NewRule();
            var mode = new EcaExecutionMode(EcaExecutionModeOverlap.Allow);
            Func<int, EcaExecutionGroupState, State> create = (value, state) => new State(value, state);
            var checker = new EcaBaseConditionChecker();
            var runner = new EcaBaseActionRunner();
            Assert.Throws<ArgumentNullException>(() => new EcaExecutionGroup<int, State>(
                null, mode, create, checker, runner));
            Assert.Throws<ArgumentNullException>(() => new EcaExecutionGroup<int, State>(
                rule, null, create, checker, runner));
            Assert.Throws<ArgumentNullException>(() => new EcaExecutionGroup<int, State>(
                rule, mode, null, checker, runner));
            Assert.Throws<ArgumentNullException>(() => new EcaExecutionGroup<int, State>(
                rule, mode, create, null, runner));
            Assert.Throws<ArgumentNullException>(() => new EcaExecutionGroup<int, State>(
                rule, mode, create, checker, null));
        }
    }
}
