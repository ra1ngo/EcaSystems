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

    public interface IEcaRule<TContext> : IEcaRule
    {
        IEcaCondition<TContext> Condition { get; }
        IEcaAction<TContext> Action { get; }
    }
}
