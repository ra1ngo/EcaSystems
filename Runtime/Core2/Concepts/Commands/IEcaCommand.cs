using System;
using System.Threading.Tasks;

namespace EcaSystems.Core2
{
    public interface IEcaCommand
    {
        string Id { get; }
        Type ContextType { get; }
        Type ArgsType { get; }
        Task Run(IEcaActionContext context, object args);
    }

    public interface IEcaCommand<in C, in A> : IEcaCommand
        where C : IEcaActionContext
    {
        Task Run(C context, A args);

        Type IEcaCommand.ContextType => typeof(C);
        Type IEcaCommand.ArgsType => typeof(A);
        Task IEcaCommand.Run(IEcaActionContext context, object args) => Run((C)context, (A)args);
    }
}
