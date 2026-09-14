using System.Threading.Tasks;

namespace EcaSystems.Core2
{
    public interface IEcaCommand
    {
        string Id { get; }
    }

    public interface IEcaCommand<in TContext, in TArgs> : IEcaCommand
        where TContext : IEcaActionContext
    {
        Task Run(TContext context, TArgs args);
    }
}
