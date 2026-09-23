using System;
using System.Threading.Tasks;

namespace EcaSystems.Core2
{
    internal sealed class EcaRuleCreator
    {
        private readonly IEcaEventRegistry _events;
        private readonly IEcaCommands _commands;
        private readonly IEcaStateResolver _stateResolver;

        internal EcaRuleCreator(IEcaEventRegistry events, IEcaCommands commands, IEcaStateResolver stateResolver)
        {
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            _stateResolver = stateResolver ?? throw new ArgumentNullException(nameof(stateResolver));
        }

        internal EcaRule<E, R> Create<E, R, C, A>(string id, string eventId)
            where R : IEcaRuleState<E>
            where C : AEcaCondition<R>, new()
            where A : AEcaAction<R>, new()
        {
            var ecaEvent = Resolve<E>(id, eventId);
            var condition = new C();
            condition.Initialize(_stateResolver);
            var action = new A();
            action.Initialize(_commands, _stateResolver);
            return new EcaRule<E, R>(id, ecaEvent, condition, action);
        }

        internal EcaRule<E, R> Create<E, R, A>(string id, string eventId)
            where R : IEcaRuleState<E>
            where A : AEcaAction<R>, new()
        {
            var ecaEvent = Resolve<E>(id, eventId);
            var action = new A();
            action.Initialize(_commands, _stateResolver);
            return new EcaRule<E, R>(id, ecaEvent, null, action);
        }

        internal EcaRule<E, R> Create<E, R>(string id, string eventId,
            Func<R, IEcaActionContext, IEcaCommands, Task> action,
            Func<R, IEcaConditionContext, bool> condition)
            where R : IEcaRuleState<E>
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            var ecaEvent = Resolve<E>(id, eventId);
            var wrappedAction = new DelegateAction<R>(id + ".action", action);
            wrappedAction.Initialize(_commands, _stateResolver);
            var wrappedCondition = condition == null ? null : new DelegateCondition<R>(id + ".condition", condition);
            wrappedCondition?.Initialize(_stateResolver);
            return new EcaRule<E, R>(id, ecaEvent, wrappedCondition, wrappedAction);
        }

        private IEcaEvent<E> Resolve<E>(string id, string eventId)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Rule id cannot be empty.", nameof(id));
            var ecaEvent = _events.Resolve(eventId);
            if (ecaEvent is not IEcaEvent<E> typed || ecaEvent.EventStateType != typeof(E))
                throw new ArgumentException($"Event '{eventId}' is incompatible with event state '{typeof(E)}'.", nameof(eventId));
            return typed;
        }

        private sealed class DelegateCondition<R> : AEcaCondition<R> where R : IEcaRuleState
        {
            private readonly Func<R, IEcaConditionContext, bool> _check;
            public override string Id { get; }
            internal DelegateCondition(string id, Func<R, IEcaConditionContext, bool> check) { Id = id; _check = check; }
            public override bool Check(R state, IEcaConditionContext context) => _check(state, context);
        }

        private sealed class DelegateAction<R> : AEcaAction<R> where R : IEcaRuleState
        {
            private readonly Func<R, IEcaActionContext, IEcaCommands, Task> _run;
            public override string Id { get; }
            internal DelegateAction(string id, Func<R, IEcaActionContext, IEcaCommands, Task> run) { Id = id; _run = run; }
            public override Task Run(R state, IEcaActionContext context) => _run(state, context, Commands);
        }
    }
}
