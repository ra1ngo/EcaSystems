namespace EcaSystems.Core
{
    public interface IEcaRuleChecker
    {
        bool Check<TContext>(IEcaRule<TContext> rule, TContext context);
    }
}
