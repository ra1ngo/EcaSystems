using System.Threading.Tasks;

namespace EcaSystems.Core2
{
    public interface IEcaCommand
    {
        string Id { get; }
    }

    public interface IEcaCommand<in C, in A> : IEcaCommand
        where C : IEcaActionContext
    {
        Task Run(C context, A args);
    }
}
