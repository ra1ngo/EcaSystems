using System;
using System.Threading.Tasks;

namespace EcaSystems.Core1
{
    public sealed class EcaRuleRunner : IEcaRuleRunner
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

        public IEcaRuleRun CreateRun(IEcaRule rule, EcaEventOccurrence occurrence)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            if (occurrence == null) throw new ArgumentNullException(nameof(occurrence));
            return occurrence.Accept(new CreateRequest(this, rule));
        }

        public bool Check(IEcaRuleRun run)
        {
            var data = RequireOwned(run);
            return data.Bridge.Check(this, data);
        }

        public Task RunAction(IEcaRuleRun run)
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

        private IEcaRuleRun CreateTyped<T>(IEcaRule rule, IEcaEvent<T> ecaEvent, T eventState)
        {
            if (rule.Event.Id != ecaEvent.Id || rule.Event.EventStateType != typeof(T) ||
                !(rule is IEcaRule<T, EcaRuleState<T>, IEcaConditionRunnerContext, IEcaActionRunnerContext> typed))
                throw new ArgumentException($"Rule '{rule.Id}' is incompatible with the Base runner for '{typeof(T)}'.", nameof(rule));
            return new RuleRun<T>(this, typed, new EcaRuleState<T>(eventState));
        }

        private bool CheckTyped<T>(RuleRun<T> run) => run.Rule.Condition == null ||
            _conditions.Check(run.Rule.Condition, run.State, _conditionContext);

        private Task RunTyped<T>(RuleRun<T> run) => _actions.Run(run.Rule.Action, run.State, _actionContext);

        /* C# cannot recover an unknown closed generic argument from IEcaRuleRun.
           The paired stateless bridge restores T; all ECA execution stays above.
           Rule and token have no Check/Run/Accept methods. Nothing is cached per Fire. */
        private interface IRunBridge
        {
            bool Check(EcaRuleRunner runner, RuleRun run);
            Task RunAction(EcaRuleRunner runner, RuleRun run);
        }

        private sealed class RunBridge<T> : IRunBridge
        {
            internal static readonly RunBridge<T> Instance = new();
            public bool Check(EcaRuleRunner runner, RuleRun run) => runner.CheckTyped((RuleRun<T>)run);
            public Task RunAction(EcaRuleRunner runner, RuleRun run) => runner.RunTyped((RuleRun<T>)run);
        }

        private abstract class RuleRun : IEcaRuleRun
        {
            internal readonly EcaRuleRunner Owner;
            internal readonly IRunBridge Bridge;
            protected RuleRun(EcaRuleRunner owner, IRunBridge bridge) { Owner = owner; Bridge = bridge; }
        }

        private sealed class RuleRun<T> : RuleRun
        {
            internal readonly IEcaRule<T, EcaRuleState<T>, IEcaConditionRunnerContext, IEcaActionRunnerContext> Rule;
            internal readonly EcaRuleState<T> State;

            internal RuleRun(EcaRuleRunner owner,
                IEcaRule<T, EcaRuleState<T>, IEcaConditionRunnerContext, IEcaActionRunnerContext> rule,
                EcaRuleState<T> state) : base(owner, RunBridge<T>.Instance)
            {
                Rule = rule;
                State = state;
            }
        }

        /* One request per CreateRun, never a mutable 'current rule' on the runner. */
        private sealed class CreateRequest : IEcaEventOccurrenceVisitor<IEcaRuleRun>
        {
            private readonly EcaRuleRunner _owner;
            private readonly IEcaRule _rule;
            internal CreateRequest(EcaRuleRunner owner, IEcaRule rule) { _owner = owner; _rule = rule; }
            public IEcaRuleRun Visit<T>(IEcaEvent<T> ecaEvent, T eventState) => _owner.CreateTyped(_rule, ecaEvent, eventState);
        }

        private sealed class ConditionRunnerContext : IEcaConditionRunnerContext { }
        private sealed class ActionRunnerContext : IEcaActionRunnerContext { }
    }
}
