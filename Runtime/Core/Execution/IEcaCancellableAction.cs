using System.Threading.Tasks;

namespace EcaSystems.Core
{
    public interface IEcaCancellableAction<in TContext>
    {
        Task Cancel(TContext context);
    }
}
