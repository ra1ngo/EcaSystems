using System;
using System.Threading.Tasks;

namespace EcaSystems.Core1
{
    public class EcaRuleRunner : IEcaRuleRunner
    {
        private readonly EcaConditionRunner _conditions;
        private readonly EcaActionRunner _actions;
        private readonly IEcaConditionRunnerContext _conditionContext = new ConditionRunnerContext();
        private readonly IEcaActionRunnerContext _actionContext = new ActionRunnerContext();

        public EcaRuleRunner() : this(new EcaConditionRunner(), new EcaActionRunner()) { }

        public EcaRuleRunner(EcaConditionRunner conditions, EcaActionRunner actions)
        {
            _conditions = conditions ?? throw new ArgumentNullException(nameof(conditions));
            _actions = actions ?? throw new ArgumentNullException(nameof(actions));
        }

        public void ValidateRule<TEventState>(IEcaRule rule)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            if (rule.Event == null || rule.Event.EventStateType != typeof(TEventState))
                throw new ArgumentException("Rule event has an incompatible state type.", nameof(rule));
            ValidateTyped<TEventState>(rule);
        }

        protected virtual void ValidateTyped<T>(IEcaRule rule)
        {
            if (!(rule is IEcaRule<T, EcaRuleState<T>, IEcaConditionRunnerContext, IEcaActionRunnerContext>))
                throw new ArgumentException($"Rule '{rule.Id}' is incompatible with the Base runner.", nameof(rule));
        }

        public virtual IEcaRuleRun CreateRun(IEcaRule rule, EcaEventOccurrence occurrence)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            if (occurrence == null) throw new ArgumentNullException(nameof(occurrence));
            if (rule.Event == null || rule.Event.Id != occurrence.Event.Id)
                throw new ArgumentException("Rule does not match the event occurrence.", nameof(rule));
            return occurrence.Accept(new CreateRequest(this, rule));
        }

        protected virtual IEcaRuleRun CreateTyped<T>(IEcaRule rule, IEcaEvent<T> ecaEvent, T eventState)
        {
            var typed = (IEcaRule<T, EcaRuleState<T>, IEcaConditionRunnerContext, IEcaActionRunnerContext>)rule;
            return CreateRunCore(typed, new EcaRuleState<T>(eventState), _conditionContext, _actionContext);
        }

        /* Единый мост для всей вертикали: слой уже создал State и infrastructure,
           а этот метод закрывает их типы вместе с типизированной Rule. */
        protected IEcaRuleRun CreateRunCore<T, TState, TConditionContext, TActionContext>(
            IEcaRule<T, TState, TConditionContext, TActionContext> rule,
            TState state, TConditionContext conditionContext, TActionContext actionContext)
            where TState : IEcaRuleState<T>
            where TConditionContext : IEcaConditionRunnerContext
            where TActionContext : IEcaActionRunnerContext
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            if (state is null) throw new ArgumentNullException(nameof(state));
            if (conditionContext is null) throw new ArgumentNullException(nameof(conditionContext));
            if (actionContext is null) throw new ArgumentNullException(nameof(actionContext));
            return new RuleRun<T, TState, TConditionContext, TActionContext>(this, rule, state, conditionContext, actionContext);
        }

        public virtual bool Check(IEcaRuleRun run)
        {
            var data = RequireOwned(run);
            return data.Bridge.Check(this, data);
        }

        public virtual Task RunAction(IEcaRuleRun run)
        {
            var data = RequireOwned(run);
            return data.Bridge.RunAction(this, data);
        }

        private RuleRun RequireOwned(IEcaRuleRun run)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (!(run is RuleRun data) || !ReferenceEquals(data.Owner, this))
                throw new ArgumentException("RuleRun belongs to a different runner.", nameof(run));
            return data;
        }

        private bool CheckTyped<T, TS, TC, TA>(RuleRun<T, TS, TC, TA> run)
            where TS : IEcaRuleState<T>
            where TC : IEcaConditionRunnerContext
            where TA : IEcaActionRunnerContext => run.Rule.Condition == null ||
                _conditions.Check(run.Rule.Condition, run.State, run.ConditionContext);

        private Task RunTyped<T, TS, TC, TA>(RuleRun<T, TS, TC, TA> run)
            where TS : IEcaRuleState<T>
            where TC : IEcaConditionRunnerContext
            where TA : IEcaActionRunnerContext => _actions.Run(run.Rule.Action, run.State, run.ActionContext);

        /* C# не восстанавливает неизвестные generic-параметры из opaque interface.
           Закрытый token создаётся только со своим stateless bridge; после проверки
           владельца приведение безопасно. Выполнение остаётся в RuleRunner. */
        private interface IRunBridge
        {
            bool Check(EcaRuleRunner runner, RuleRun run);
            Task RunAction(EcaRuleRunner runner, RuleRun run);
        }

        private sealed class RunBridge<T, TS, TC, TA> : IRunBridge
            where TS : IEcaRuleState<T>
            where TC : IEcaConditionRunnerContext
            where TA : IEcaActionRunnerContext
        {
            internal static readonly RunBridge<T, TS, TC, TA> Instance = new();
            public bool Check(EcaRuleRunner runner, RuleRun run) => runner.CheckTyped((RuleRun<T, TS, TC, TA>)run);
            public Task RunAction(EcaRuleRunner runner, RuleRun run) => runner.RunTyped((RuleRun<T, TS, TC, TA>)run);
        }

        private abstract class RuleRun : IEcaRuleRun
        {
            internal readonly EcaRuleRunner Owner;
            internal readonly IRunBridge Bridge;
            protected RuleRun(EcaRuleRunner owner, IRunBridge bridge) { Owner = owner; Bridge = bridge; }
        }

        private sealed class RuleRun<T, TS, TC, TA> : RuleRun
            where TS : IEcaRuleState<T>
            where TC : IEcaConditionRunnerContext
            where TA : IEcaActionRunnerContext
        {
            internal readonly IEcaRule<T, TS, TC, TA> Rule;
            internal readonly TS State;
            internal readonly TC ConditionContext;
            internal readonly TA ActionContext;

            internal RuleRun(EcaRuleRunner owner, IEcaRule<T, TS, TC, TA> rule, TS state, TC conditionContext, TA actionContext)
                : base(owner, RunBridge<T, TS, TC, TA>.Instance)
            {
                Rule = rule;
                State = state;
                ConditionContext = conditionContext;
                ActionContext = actionContext;
            }
        }

        /* Отдельный запрос на каждый CreateRun: общего изменяемого current rule нет. */
        private sealed class CreateRequest : IEcaEventOccurrenceVisitor<IEcaRuleRun>
        {
            private readonly EcaRuleRunner _owner;
            private readonly IEcaRule _rule;
            internal CreateRequest(EcaRuleRunner owner, IEcaRule rule) { _owner = owner; _rule = rule; }
            public IEcaRuleRun Visit<T>(IEcaEvent<T> ecaEvent, T eventState)
            {
                _owner.ValidateRule<T>(_rule);
                return _owner.CreateTyped(_rule, ecaEvent, eventState);
            }
        }

        private sealed class ConditionRunnerContext : IEcaConditionRunnerContext { }
        private sealed class ActionRunnerContext : IEcaActionRunnerContext { }
    }
}
