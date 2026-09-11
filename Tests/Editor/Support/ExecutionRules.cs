using EcaSystems.Core;

namespace EcaSystems.Tests.Support
{
    // Existing smoke-test construction helper; this is not a production Rule API.
    internal static class ExecutionRules
    {
        internal static EcaRule<TEventContext, IEcaExecutionConditionContext<TEventContext>, IEcaExecutionActionContext<TEventContext>> CreateExecutionRule<TEventContext>(
            string id, EcaEvent<TEventContext> ecaEvent,
            IEcaAction<IEcaExecutionActionContext<TEventContext>> action,
            IEcaCondition<IEcaExecutionConditionContext<TEventContext>> condition = null)
        {
            return new EcaRule<TEventContext, IEcaExecutionConditionContext<TEventContext>, IEcaExecutionActionContext<TEventContext>>(
                new EcaRuleConfig<TEventContext, IEcaExecutionConditionContext<TEventContext>, IEcaExecutionActionContext<TEventContext>>
                {
                    Id = id, Name = id, Event = ecaEvent, Action = action, Condition = condition
                });
        }
    }
}
