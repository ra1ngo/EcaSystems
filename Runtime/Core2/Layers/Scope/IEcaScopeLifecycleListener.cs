namespace EcaSystems.Core2
{
    // Each callback must be atomic for the listener's own bindings on failure.
    public interface IEcaScopeLifecycleListener
    {
        void ConnectScope(EcaScope scope);
        void DisconnectScope(EcaScope scope);
        void ConnectRule(EcaScope scope, IEcaRule rule);
        void DisconnectRule(EcaScope scope, IEcaRule rule);
    }
}
