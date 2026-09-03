using EcaRuleId = System.String;

namespace EcaSystems.Core
{
    public interface IEcaRule<TContext>
    {
        EcaRuleId Id { get; }

        string Name { get; }

        string Description { get; }

        IEcaEvent Event { get; }

        IEcaCondition<TContext> Condition { get; }

        IEcaAction<TContext> Action { get; }
    }
}