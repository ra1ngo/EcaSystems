using System.Threading;
using System.Threading.Tasks;

namespace EcaSystems.Core
{
    public interface IEcaRuleRunner
    {
        Task Run<TContext>(
            IEcaRule<TContext> rule,
            TContext context,
            CancellationToken cancellationToken
        );
    }
}