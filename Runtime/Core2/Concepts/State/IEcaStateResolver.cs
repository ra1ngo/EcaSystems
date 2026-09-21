namespace EcaSystems.Core2
{
    public interface IEcaStateResolver
    {
        T Resolve<T>(IEcaRuleState ruleState);
    }
}
