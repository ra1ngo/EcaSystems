using System.Threading.Tasks;

namespace EcaSystems.Core
{
    public interface IEcaExecutionExecutor
    {
        Task Execute<TEventContext>(EcaRuleExecution<TEventContext> execution);
        Task Cancel<TEventContext>(EcaRuleExecution<TEventContext> execution);
    }
}
