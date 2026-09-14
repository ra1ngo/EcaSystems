using System;

namespace EcaSystems.Core2
{
    public interface IEcaEvent
    {
        string Id { get; }
        string Name { get; }
        string Description { get; }
        Type EventStateType { get; }
    }

    public interface IEcaEvent<E> : IEcaEvent { }
}
