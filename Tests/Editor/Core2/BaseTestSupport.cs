using System;
using System.Threading.Tasks;
using EcaSystems.Core2;

namespace EcaSystems.Tests.Core2
{
    internal static class BaseTestSupport
    {
        internal sealed class Event<E> : IEcaEvent<E>
        {
            public string Id { get; set; } = "event";
            public string Name => Id;
            public string Description => Id;
            public Type EventStateType { get; set; } = typeof(E);
        }

        internal sealed class State : IEcaRuleState<int>
        {
            public int EventState { get; set; }
        }

        internal sealed class OtherState : IEcaRuleState<int>
        {
            public int EventState => 0;
        }

        internal sealed class ConditionContext : IEcaConditionContext { }
        internal sealed class ActionContext : IEcaActionContext { }
        internal sealed class OtherConditionContext : IEcaConditionContext { }
        internal sealed class OtherActionContext : IEcaActionContext { }

        internal sealed class Condition : IEcaCondition<State>
        {
            public string Id => "condition";
            public string Name => Id;
            public string Description => Id;
            public Func<State, ConditionContext, bool> CheckHandler { get; set; } = (state, context) => true;
            public bool Check(State state, IEcaConditionContext context) => CheckHandler(state, (ConditionContext)context);
        }

        internal sealed class Action : IEcaAction<State>
        {
            public string Id => "action";
            public string Name => Id;
            public string Description => Id;
            public Func<State, ActionContext, Task> RunHandler { get; set; } = (state, context) => Task.CompletedTask;
            public Task Run(State state, IEcaActionContext context) => RunHandler(state, (ActionContext)context);
        }

        internal sealed class Rule : IEcaRule<int, State>
        {
            public string Id { get; set; } = "rule";
            public string Name => Id;
            public string Description => Id;
            public IEcaEvent<int> Event { get; set; }
            public IEcaCondition<State> Condition { get; set; }
            public IEcaAction<State> Action { get; set; } = new Action();
            IEcaEvent IEcaRule.Event => Event;
            IEcaCondition IEcaRule.Condition => Condition;
            IEcaAction IEcaRule.Action => Action;
        }
    }
}
