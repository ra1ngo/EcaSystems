using System.Collections.Generic;

namespace EcaSystems.Core2
{
    public interface IEcaEventRegistry
    {
        IReadOnlyCollection<IEcaEvent> Events { get; }
        void Register(IEcaEvent ecaEvent);
        bool Unregister(string eventId);
        bool Contains(string eventId);
        IEcaEvent Resolve(string eventId);
        bool CheckRegistered(IEcaEvent ecaEvent);
    }
}
