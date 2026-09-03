using System;

namespace EcaSystems.Core
{
    public interface IEcaContext<out TEventContext>
    {
        TEventContext EventContext { get; }
    }
}