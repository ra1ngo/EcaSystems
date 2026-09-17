namespace EcaSystems.Core2
{
    public interface IEcaEventRegistry
    {
        void Register(IEcaEvent ecaEvent);
        void Register<E>(IEcaEvent<E> ecaEvent);
        bool Unregister(string eventId);
        bool Contains(string eventId);

        bool CheckRegistered(IEcaEvent ecaEvent);
    }
}
