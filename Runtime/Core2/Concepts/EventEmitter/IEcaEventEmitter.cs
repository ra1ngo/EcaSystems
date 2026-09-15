namespace EcaSystems.Core2
{
    public interface IEcaEventEmitter
    {
        void Fire<E>(IEcaEvent<E> ecaEvent, E eventState);
    }
}
