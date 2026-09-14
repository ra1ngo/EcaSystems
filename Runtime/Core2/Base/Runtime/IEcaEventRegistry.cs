namespace EcaSystems.Core2
{
    public interface IEcaEventRegistry
    {
        void Register<TEventState>(IEcaEvent<TEventState> ecaEvent);

        bool CheckRegistered(IEcaEvent ecaEvent);
    }
}