namespace EcaSystems.Core2
{
    // Callbacks are atomic for the connector's own bindings; external durable state remains externally owned.
    public interface IEcaSystemLifecycleConnector
    {
        void ConnectScope(EcaScope scope);
        void DisconnectScope(EcaScope scope);
        void ConnectRule(EcaScope scope, IEcaRule rule);
        void DisconnectRule(EcaScope scope, IEcaRule rule);
    }
}
