using System;

namespace EcaSystems.Core1
{
    public sealed class EcaScopeRuleRunner : EcaExecutionRuleRunner
    {
        private readonly EcaScopeState _scopeState;
        private readonly IEcaScopeConditionRunnerContext _conditionContext = new ConditionRunnerContext();
        private readonly IEcaScopeActionRunnerContext _actionContext = new ActionRunnerContext();

        public EcaScopeRuleRunner(EcaRuleExecutionRegistry groups, EcaScopeState scopeState) : base(groups)
        {
            _scopeState = scopeState ?? throw new ArgumentNullException(nameof(scopeState));
        }

        protected override void ValidateTyped<T>(IEcaRule rule)
        {
            if (!(rule is IEcaRule<T, EcaScopeRuleState<T>, IEcaScopeConditionRunnerContext, IEcaScopeActionRunnerContext>))
                throw new ArgumentException($"Rule '{rule.Id}' is incompatible with the Scope runner.", nameof(rule));
        }

        protected override IEcaRuleRun CreateTyped<T>(IEcaRule rule, IEcaEvent<T> ecaEvent, T eventState)
        {
            var typed = (IEcaRule<T, EcaScopeRuleState<T>, IEcaScopeConditionRunnerContext, IEcaScopeActionRunnerContext>)rule;
            var group = GetGroup(rule);
            var state = new EcaScopeRuleState<T>(eventState, group.State, _scopeState);
            /* Мост принадлежит Base, обёртка и admission/lifecycle — Execution.
               Scope расширяет только типизированные данные и infrastructure. */
            return WrapRun(CreateRunCore(typed, state, _conditionContext, _actionContext), group);
        }

        private sealed class ConditionRunnerContext : IEcaScopeConditionRunnerContext { }
        private sealed class ActionRunnerContext : IEcaScopeActionRunnerContext { }
    }
}
