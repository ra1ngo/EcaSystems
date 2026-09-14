using System.Threading.Tasks;

namespace EcaSystems.Core2
{
    public interface IEcaActionRunner
    {
        Task Run<R, A>(IEcaAction<R, A> action, R state, A context)
            where R : IEcaRuleState
            where A : IEcaActionContext;
    }
}
