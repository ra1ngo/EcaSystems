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
            public System.Action Callback;
            public void Handle<E>(IEcaEvent<E> ecaEvent, E eventState)
            {
                Types.Add(typeof(E));
                Events.Add(ecaEvent);
                States.Add(eventState);
                Callback?.Invoke();
            }
        }

        internal sealed class Source : IEcaEventOccurrenceSource
        {
            private Action<EcaEventOccurrence> _fired;
            public int Adds { get; private set; }
            public int Removes { get; private set; }
            public event Action<EcaEventOccurrence> Fired
            {
                add { Adds++; _fired += value; }
                remove { Removes++; _fired -= value; }
            }
            public void Emit(EcaEventOccurrence occurrence) => _fired?.Invoke(occurrence);
            public Action<EcaEventOccurrence> Snapshot() => _fired;
        }

        internal sealed class Occurrence<E> : EcaEventOccurrence
        {
            private readonly IEcaEvent<E> _event;
            private readonly E _state;
            public int AcceptCalls { get; private set; }
            public Occurrence(IEcaEvent<E> ecaEvent, E state) : base(ecaEvent) { _event = ecaEvent; _state = state; }
            public override void Accept(IEcaEventHandler handler) { AcceptCalls++; handler.Handle(_event, _state); }
        }

        internal sealed class State<E> : IEcaRuleState<E>
        {
            public E EventState { get; }
            public State(E eventState) => EventState = eventState;
        }

        internal sealed class Condition<E> : IEcaCondition<State<E>, BaseTestSupport.ConditionContext>
        {
            public string Id => "condition";
            public string Name => Id;
            public string Description => Id;
            public Func<E, bool> Handler = state => true;
            public bool Check(State<E> state, BaseTestSupport.ConditionContext context) => Handler(state.EventState);
        }

        internal sealed class EventAction<E> : IEcaAction<State<E>, BaseTestSupport.ActionContext>
        {
            public string Id => "action";
            public string Name => Id;
            public string Description => Id;
            public Func<E, Task> Handler = state => Task.CompletedTask;
            public Task Run(State<E> state, BaseTestSupport.ActionContext context) => Handler(state.EventState);
        }

        internal sealed class Rule<E> : IEcaRule<E, State<E>, BaseTestSupport.ConditionContext, BaseTestSupport.ActionContext>
        {
            public string Id { get; set; } = "rule";
            public string Name => Id;
            public string Description => Id;
            public IEcaEvent<E> Event { get; set; }
            public IEcaCondition<State<E>, BaseTestSupport.ConditionContext> Condition { get; set; }
            public IEcaAction<State<E>, BaseTestSupport.ActionContext> Action { get; set; }
            IEcaEvent IEcaRule.Event => Event;
            IEcaCondition IEcaRule.Condition => Condition;
            IEcaAction IEcaRule.Action => Action;
        }
    }

    internal sealed class EcaTestBaseRuntime : EcaBaseRuntime, IEcaEventHandler, IDisposable
    {
        private readonly EcaEventReceiver _receiver;
        private readonly BaseTestSupport.ConditionContext _conditions = new();
        private readonly BaseTestSupport.ActionContext _actions = new();

        public EcaTestBaseRuntime(EcaBaseEventRegistry events, EcaEventReceiver receiver)
            : base(new EcaBaseRuleRegistry(events), new EcaBaseActionRunner(), new EcaBaseConditionChecker())
        {
            _receiver = receiver;
            receiver.Subscribe(this);
        }

        void IEcaEventHandler.Handle<E>(IEcaEvent<E> ecaEvent, E eventState) => Fire(ecaEvent, eventState);

        public void Fire<E>(IEcaEvent<E> ecaEvent, E eventState) =>
            base.Fire<E, EventTestSupport.State<E>, BaseTestSupport.ConditionContext, BaseTestSupport.ActionContext>(
                ecaEvent, eventState, (rule, state) => new EventTestSupport.State<E>(state), _conditions, _actions);

        public void Dispose() => _receiver.Unsubscribe(this);
    }
}
