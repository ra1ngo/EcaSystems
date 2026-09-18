using System;
using EcaSystems.Core2;

namespace EcaSystems.Time.Eca
{
    internal sealed class EcaTimeEvent : IEcaEvent<EcaTimeEventState>
    {
        public string Id { get; }
        public string Name { get; }
        public string Description => Name;
        public Type EventStateType => typeof(EcaTimeEventState);

        internal EcaTimeEvent(EcaTimeEventKey id, string name)
        {
            Id = EcaTimeEventIds.Get(id);
            Name = name;
        }
    }
}
