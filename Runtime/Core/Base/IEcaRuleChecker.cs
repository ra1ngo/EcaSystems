namespace EcaSystems.Core
{
    public interface IEcaRuleChecker
    {
        bool Check<TEventContext, TConditionContext, TActionContext>(IEcaRule<TEventContext, TConditionContext, TActionContext> rule, TConditionContext context)
            where TConditionContext : IEcaConditionContext<TEventContext>
            where TActionContext : IEcaActionContext<TEventContext>;
    }
}
