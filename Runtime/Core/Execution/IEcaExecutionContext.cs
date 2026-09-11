namespace EcaSystems.Core
{
    /* Общие данные Execution не задают роль Condition или Action.
       Контракт invariant, как и role-контексты: payload остаётся точным типом Rule. */
    public interface IEcaExecutionContext<TEventContext> : IEcaContext<TEventContext>
    {
        EcaRuleExecutionGroupState RuleExecutionGroupState { get; }
    }
}
