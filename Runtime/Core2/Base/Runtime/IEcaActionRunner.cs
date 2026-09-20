using System.Threading.Tasks;

namespace EcaSystems.Core2
{
    public interface IEcaActionRunner
    {
        Task Run<R>(IEcaAction<R> action, R state, IEcaActionContext context)
            where R : IEcaRuleState;
    }
}
