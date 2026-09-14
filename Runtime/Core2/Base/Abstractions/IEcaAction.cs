using System.Threading.Tasks;

namespace EcaSystems.Core2
{
    public interface IEcaAction
    {
        string Id { get; }
        string Name { get; }
        string Description { get; }
    }

    public interface IEcaAction<in R, in A> : IEcaAction
        where R : IEcaRuleState
        where A : IEcaActionContext
    {
        Task Run(R state, A context);
    }
}
