using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EcaSystems.Core2;

namespace EcaSystems.Tests.Core2
{
    internal static class EventTestSupport
    {
        internal class UserMessage { public string Text { get; set; } }
        internal sealed class DerivedMessage : UserMessage { }
        internal readonly struct UserScore
        {
            public int Value { get; }
            public UserScore(int value) => Value = value;
        }

        internal sealed class RecordingHandler : IEcaEventHandler
        {
            // Только наблюдение в тесте; generic callback доходит сюда без object adapter.
            public readonly List<Type> Types = new();
            public readonly List<IEcaEvent> Events = new();
            public readonly List<object> States = new();
            public readonly List<IEcaConditionContext> ConditionContexts = new();
            public readonly List<IEcaActionContext> ActionContexts = new();
            public System.Action Callback;
            public void Handle<E>(IEcaEvent<E> ecaEvent, E eventState, IEcaConditionContext conditionContext, IEcaActionContext actionContext)
            {
                Types.Add(typeof(E));
                Events.Add(ecaEvent);
                States.Add(eventState);
                ConditionContexts.Add(conditionContext);
                ActionContexts.Add(actionContext);
                Callback?.Invoke();
            }
        }

        internal sealed class State<E> : IEcaRuleState<E>
        {
            public string RuleId { get; set; } = "rule";
            public E EventState { get; }
            public State(E eventState) => EventState = eventState;
        }

        internal sealed class Condition<E> : IEcaCondition<State<E>>
        {
            public string Id => "condition";
            public string Name => Id;
            public string Description => Id;
            public Func<E, bool> Handler = state => true;
            public bool Check(State<E> state, IEcaConditionContext context) => Handler(state.EventState);
        }

        internal sealed class EventAction<E> : IEcaAction<State<E>>
        {
            public string Id => "action";
            public string Name => Id;
            public string Description => Id;
            public Func<E, Task> Handler = state => Task.CompletedTask;
            public Task Run(State<E> state, IEcaActionContext context) => Handler(state.EventState);
        }

        internal sealed class Rule<E> : IEcaRule<E, State<E>>
        {
            public string Id { get; set; } = "rule";
            public string Name => Id;
            public string Description => Id;
            public IEcaEvent<E> Event { get; set; }
            public IEcaCondition<State<E>> Condition { get; set; }
            public IEcaAction<State<E>> Action { get; set; }
            IEcaEvent IEcaRule.Event => Event;
            IEcaCondition IEcaRule.Condition => Condition;
            IEcaAction IEcaRule.Action => Action;
        }
    }

    internal sealed class EcaTestBaseRuntime : EcaBaseRuntime, IEcaEventHandler
    {

        public EcaTestBaseRuntime(EcaBaseEventRegistry events, EcaEventEmitter emitter)
            : base(new EcaBaseRuleRegistry(events), new EcaBaseActionRunner(), new EcaBaseConditionChecker())
        {
            emitter.Bind(this);
        }

        void IEcaEventHandler.Handle<E>(IEcaEvent<E> ecaEvent, E eventState, IEcaConditionContext conditionContext, IEcaActionContext actionContext) => Fire(ecaEvent, eventState, conditionContext, actionContext);

        public void Fire<E>(IEcaEvent<E> ecaEvent, E eventState, IEcaConditionContext conditionContext = null, IEcaActionContext actionContext = null) =>
            base.ForceFire<E, EventTestSupport.State<E>>(
                ecaEvent, eventState, (rule, state) => new EventTestSupport.State<E>(state) { RuleId = rule.Id }, conditionContext, actionContext);
    }
}
