using System.Threading.Tasks;

namespace EcaSystems.Core2
{
    public interface IEcaCommands
    {
        Task Run<R, A>(string commandId, R state, IEcaActionContext context, A args)
            where R : IEcaRuleState;
    }
}
