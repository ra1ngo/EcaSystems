namespace EcaSystems.Core2
{
    public interface IEcaEventRegistry
    {
        void Register<E>(IEcaEvent<E> ecaEvent);

        bool CheckRegistered(IEcaEvent ecaEvent);
    }
}
