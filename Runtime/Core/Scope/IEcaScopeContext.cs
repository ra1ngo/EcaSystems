namespace EcaSystems.Core
{
    /* Контракт следующего слоя данных. Текущий Scope.Fire ещё создаёт только
       Execution-контексты; создание расширенных контекстов требует отдельного
       архитектурного решения. Execution не должен зависеть от Scope. */
    public interface IEcaScopeContext<TEventContext> : IEcaExecutionContext<TEventContext>
    {
        EcaScopeState ScopeState { get; }
    }
}
