using EcaRuleId = System.String;

namespace EcaSystems.Core
{
    public sealed class EcaRuleConfig<TContext>
    {
        public EcaRuleId Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; } = "";
        public IEcaEvent Event { get; set; }
        public IEcaCondition<TContext> Condition { get; set; }
        public IEcaAction<TContext> Action { get; set; }
    }
}