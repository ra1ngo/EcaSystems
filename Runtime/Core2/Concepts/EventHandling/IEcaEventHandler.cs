namespace EcaSystems.Core2
{
    public interface IEcaEventHandler
    {
        void Handle<E>(IEcaEvent<E> ecaEvent, E eventState);
    }
}
