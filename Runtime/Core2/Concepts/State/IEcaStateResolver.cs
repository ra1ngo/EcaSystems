namespace EcaSystems.Core2
{
    public interface IEcaStateResolver
    {
        T Resolve<T>(string stateId, IEcaRuleState ruleState);
        T Resolve<T>(string stateId, IEcaRuleState ruleState, object payload);
    }
}
