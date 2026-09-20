namespace EcaSystems.Core2
{
    internal interface IEcaEventHandler
    {
        void Handle<E>(IEcaEvent<E> ecaEvent, E eventState,
            IEcaConditionContext conditionContext, IEcaActionContext actionContext);
    }
}
