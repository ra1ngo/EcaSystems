using System;
using System.Threading.Tasks;

namespace EcaSystems.Core1
{
    public class EcaExecutionRuleRunner : EcaRuleRunner
    {
        private readonly EcaRuleExecutionRegistry _groups;
        private readonly IEcaExecutionConditionRunnerContext _conditionContext = new ConditionRunnerContext();
        private readonly IEcaExecutionActionRunnerContext _actionContext = new ActionRunnerContext();

        public EcaExecutionRuleRunner(EcaRuleExecutionRegistry groups)
        {
            _groups = groups ?? throw new ArgumentNullException(nameof(groups));
        }

        protected override void ValidateTyped<T>(IEcaRule rule)
        {
            if (!(rule is IEcaRule<T, EcaExecutionRuleState<T>, IEcaExecutionConditionRunnerContext, IEcaExecutionActionRunnerContext>))
                throw new ArgumentException($"Rule '{rule.Id}' is incompatible with the Execution runner.", nameof(rule));
        }

        protected override IEcaRuleRun CreateTyped<T>(IEcaRule rule, IEcaEvent<T> ecaEvent, T eventState)
        {
            var typed = (IEcaRule<T, EcaExecutionRuleState<T>, IEcaExecutionConditionRunnerContext, IEcaExecutionActionRunnerContext>)rule;
            var group = GetGroup(rule);
            var state = new EcaExecutionRuleState<T>(eventState, group.State);
            return WrapRun(CreateRunCore(typed, state, _conditionContext, _actionContext), group);
        }

        protected EcaRuleExecutionGroup GetGroup(IEcaRule rule)
        {
            var group = _groups.Get(rule.Id);
            if (!ReferenceEquals(group.Rule, rule))
                throw new ArgumentException("Execution group belongs to another rule instance.", nameof(rule));
            return group;
        }

        protected IEcaRuleRun WrapRun(IEcaRuleRun innerRun, EcaRuleExecutionGroup group)
        {
            return new ExecutionRuleRun(this,
                innerRun ?? throw new ArgumentNullException(nameof(innerRun)),
                group ?? throw new ArgumentNullException(nameof(group)));
        }

        public override bool Check(IEcaRuleRun run) => base.Check(RequireOwnedWrapper(run).InnerRun);

        public override Task RunAction(IEcaRuleRun run)
        {
            var wrapper = RequireOwnedWrapper(run);
            /* BaseEngine вызывает эту фазу только после всех Conditions.
               Group наблюдает ошибки Action; остальные passed Rules продолжаются. */
            return wrapper.Group.Run(() => base.RunAction(wrapper.InnerRun));
        }

        private ExecutionRuleRun RequireOwnedWrapper(IEcaRuleRun run)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (!(run is ExecutionRuleRun wrapper) || !ReferenceEquals(wrapper.Owner, this))
                throw new ArgumentException("RuleRun belongs to a different runner.", nameof(run));
            return wrapper;
        }

        /* Пассивная обёртка удерживает именно выбранную Group даже после Unregister.
           Scope использует эту же обёртку и унаследованные Check/RunAction. */
        private sealed class ExecutionRuleRun : IEcaRuleRun
        {
            internal readonly EcaExecutionRuleRunner Owner;
            internal readonly IEcaRuleRun InnerRun;
            internal readonly EcaRuleExecutionGroup Group;
            internal ExecutionRuleRun(EcaExecutionRuleRunner owner, IEcaRuleRun innerRun, EcaRuleExecutionGroup group)
            {
                Owner = owner;
                InnerRun = innerRun;
                Group = group;
            }
        }

        private sealed class ConditionRunnerContext : IEcaExecutionConditionRunnerContext { }
        private sealed class ActionRunnerContext : IEcaExecutionActionRunnerContext { }
    }
}
