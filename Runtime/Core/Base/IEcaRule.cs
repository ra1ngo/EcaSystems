using EcaRuleId = System.String;

namespace EcaSystems.Core
{
    public interface IEcaRule
    {
        EcaRuleId Id { get; }
        string Name { get; }
        string Description { get; }
        IEcaEvent Event { get; }
    }

    public interface IEcaRule<TEventContext, TConditionContext, TActionContext> : IEcaRule
        where TConditionContext : IEcaConditionContext<TEventContext>
        where TActionContext : IEcaActionContext<TEventContext>
    {
        IEcaCondition<TConditionContext> Condition { get; }
        IEcaAction<TActionContext> Action { get; }
    }
}
