using System;

namespace EcaSystems.Core2
{
    public interface IEcaEventOccurrenceSource
    {
        event Action<EcaEventOccurrence> Fired;
    }
}
