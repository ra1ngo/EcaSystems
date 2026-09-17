using System;
using System.Threading.Tasks;

namespace EcaSystems.Core2
{
    // Abstract classes are intentional: generic default/explicit interface implementations
    // have Unity/Mono/IL2CPP compatibility concerns. Normal virtual dispatch keeps the
    // runtime bridge portable while command authors implement only the typed Run.
    public abstract class AEcaCommand
    {
        public abstract string Id { get; }
        public abstract Type ContextType { get; }
        public abstract Type ArgsType { get; }
        public abstract Task Run(IEcaActionContext context, object args);
    }

    public abstract class AEcaCommand<C, A> : AEcaCommand
        where C : IEcaActionContext
    {
        public sealed override Type ContextType => typeof(C);
        public sealed override Type ArgsType => typeof(A);
        public abstract Task Run(C context, A args);
        public sealed override Task Run(IEcaActionContext context, object args) => Run((C)context, (A)args);
    }
}
