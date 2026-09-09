using System.Threading.Tasks;

namespace EcaSystems.Core
{
    public interface IEcaRuleRunner
    {
        Task Run<TEventContext, TConditionContext, TActionContext>(
            IEcaRule<TEventContext, TConditionContext, TActionContext> rule,
            TActionContext context
        )
            where TConditionContext : IEcaConditionContext<TEventContext>
            where TActionContext : IEcaActionContext<TEventContext>;
    }
}
