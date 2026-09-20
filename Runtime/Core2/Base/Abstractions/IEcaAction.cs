using System.Threading.Tasks;

namespace EcaSystems.Core2
{
    public interface IEcaAction
    {
        string Id { get; }
        string Name { get; }
        string Description { get; }
    }

    public interface IEcaAction<in R> : IEcaAction
        where R : IEcaRuleState
    {
        Task Run(R state, IEcaActionContext context);
    }
}
