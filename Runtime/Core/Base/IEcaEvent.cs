using System;

namespace EcaSystems.Core
{
    public interface IEcaEvent
    {
        string Id { get; }
        string Name { get; }
        string Description { get; }

        Type EventContextType { get; }
    }
}