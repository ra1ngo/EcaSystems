using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using EcaSystems.Core2;
using NUnit.Framework;

namespace EcaSystems.Tests.Core2
{
    internal static class ExecutionTestSupport
    {
        internal sealed class State : IEcaExecutionRuleState<int>
        {
            public string RuleId { get; set; } = "rule";
            public int EventState { get; }
            public EcaExecutionGroupState ExecutionGroupState { get; }
            public string Extra => "extended state";

            public State(int value, EcaExecutionGroupState groupState)
            {
                EventState = value;
                ExecutionGroupState = groupState;
            }
        }

        internal sealed class ConditionContext : IEcaExecutionConditionContext
        {
            public string Extra => "condition context";
        }

        internal sealed class ActionContext : IEcaExecutionActionContext
        {
            public string Extra => "action context";
        }

        internal sealed class Condition<R, C> : IEcaCondition<R>
            where R : IEcaRuleState
            where C : IEcaConditionContext
        {
            public string Id => "condition";
            public string Name => Id;
            public string Description => Id;
            public Func<R, C, bool> Handler { get; set; }
            public bool Check(R state, IEcaConditionContext context) => Handler(state, (C)context);
        }

        internal sealed class Action<R, A> : IEcaAction<R>
            where R : IEcaRuleState
            where A : IEcaActionContext
        {
            public string Id => "action";
            public string Name => Id;
            public string Description => Id;
            public Func<R, A, Task> Handler { get; set; }
            public Task Run(R state, IEcaActionContext context) => Handler(state, (A)context);
        }

        internal sealed class Rule<E, R> : IEcaRule<E, R>
            where R : IEcaRuleState<E>
        {
            public string Id { get; set; } = "rule";
            public string Name => Id;
            public string Description => Id;
            public IEcaEvent<E> Event { get; set; }
            public IEcaCondition<R> Condition { get; set; }
            public IEcaAction<R> Action { get; set; }
            IEcaEvent IEcaRule.Event => Event;
            IEcaCondition IEcaRule.Condition => Condition;
            IEcaAction IEcaRule.Action => Action;
        }

        internal sealed class Gate : IDisposable
        {
            private readonly TaskCompletionSource<bool> _completion = new();
            public Task Task => _completion.Task;
            public void Complete() => _completion.TrySetResult(true);
            public void Fail(Exception exception) => _completion.TrySetException(exception);
            public void Dispose() => Complete();
        }

        internal static async Task Await(Task task)
        {
            Assert.That(await Task.WhenAny(task, Task.Delay(5000)), Is.SameAs(task), "Execution did not finish.");
            await task;
        }

        internal static async Task WaitUntil(Func<bool> condition)
        {
            var watch = Stopwatch.StartNew();
            while (!condition() && watch.ElapsedMilliseconds < 5000) await Task.Delay(1);
            Assert.That(condition(), Is.True, "Execution did not finish.");
        }
    }

    public abstract class ExecutionTestFixture
    {
        private readonly List<ExecutionTestSupport.Gate> _gates = new();
        internal BaseTestSupport.Event<int> Event;
        internal EcaBaseEventRegistry Events;
        internal EcaBaseRuleRegistry Rules;
        internal EcaExecutionGroupRegistry Groups;
        internal EcaExecutionRuntime Runtime;
        internal ExecutionTestSupport.ConditionContext Conditions;
        internal ExecutionTestSupport.ActionContext Actions;

        [SetUp]
        public void SetUpExecution()
        {
            Event = new BaseTestSupport.Event<int>();
            Events = new EcaBaseEventRegistry();
            Events.Register(Event);
            Rules = new EcaBaseRuleRegistry(Events);
            Groups = new EcaExecutionGroupRegistry();
            Runtime = new EcaExecutionRuntime(Rules, Groups, new EcaBaseConditionChecker(), new EcaBaseActionRunner());
            Conditions = new ExecutionTestSupport.ConditionContext();
            Actions = new ExecutionTestSupport.ActionContext();
        }

        [TearDown]
        public void ReleaseGates()
        {
            foreach (var gate in _gates) gate.Dispose();
            _gates.Clear();
        }

        internal ExecutionTestSupport.Gate NewGate()
        {
            var gate = new ExecutionTestSupport.Gate();
            _gates.Add(gate);
            return gate;
        }

        internal ExecutionTestSupport.Rule<int, ExecutionTestSupport.State> NewRule(
            string id = "rule", Func<ExecutionTestSupport.State, ExecutionTestSupport.ConditionContext, bool> check = null,
            Func<ExecutionTestSupport.State, ExecutionTestSupport.ActionContext, Task> run = null)
        {
            return new ExecutionTestSupport.Rule<int, ExecutionTestSupport.State>
            {
                Id = id, Event = Event,
                Condition = check == null ? null : new ExecutionTestSupport.Condition<
                    ExecutionTestSupport.State, ExecutionTestSupport.ConditionContext> { Handler = check },
                Action = new ExecutionTestSupport.Action<ExecutionTestSupport.State, ExecutionTestSupport.ActionContext>
                {
                    Handler = run ?? ((state, context) => Task.CompletedTask)
                }
            };
        }

        internal void RegisterRule(IEcaRule<int, ExecutionTestSupport.State> rule, EcaExecutionMode mode,
            Func<int, EcaExecutionGroupState, ExecutionTestSupport.State> createState)
        {
            Runtime.Register(rule, mode, createState == null ? null : (value, group) =>
            {
                var state = createState(value, group);
                state.RuleId = rule.Id;
                return state;
            });
        }

        internal void Fire(int value = 1) => Runtime.Fire<int>(Event, value, Conditions, Actions);
    }
}
