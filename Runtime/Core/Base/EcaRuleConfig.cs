using EcaRuleId = System.String;

namespace EcaSystems.Core
{
    public sealed class EcaRuleConfig<TEventContext, TConditionContext, TActionContext>
        where TConditionContext : IEcaConditionContext<TEventContext>
        where TActionContext : IEcaActionContext<TEventContext>
    {
        public EcaRuleId Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; } = "";
        public IEcaEvent Event { get; set; }
        public IEcaCondition<TConditionContext> Condition { get; set; }
        public IEcaAction<TActionContext> Action { get; set; }
    }
}