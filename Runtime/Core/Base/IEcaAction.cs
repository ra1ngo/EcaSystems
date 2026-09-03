using System.Threading;
using System.Threading.Tasks;

namespace EcaSystems.Core
{
    public interface IEcaAction<in TContext>
    {
        Task Run(TContext context, CancellationToken cancellationToken);
    }
}