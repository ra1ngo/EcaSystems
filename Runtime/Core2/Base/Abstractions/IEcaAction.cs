using System.Threading.Tasks;

namespace EcaSystems.Core2
{
    public interface IEcaAction
    {
        string Id { get; }
        string Name { get; }
        string Description { get; }
    }

    public interface IEcaAction<in TRuleState, in TActionContext> : IEcaAction
        where TRuleState : IEcaRuleState
        where TActionContext: IEcaActionContext
    {
        Task Run(TRuleState state, TActionContext context);
    }
}
