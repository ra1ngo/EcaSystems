using System.Threading.Tasks;

namespace EcaSystems.Core2
{
    public interface IEcaActionRunner
    {
        Task Run<TRuleState, TActionContext>(
            IEcaAction<TRuleState, TActionContext> action,
            TRuleState state,
            TActionContext context)
            where TRuleState : IEcaRuleState
            where TActionContext : IEcaActionContext;
    }
}