using System.Threading.Tasks;

namespace EcaSystems.Core2
{
    public interface IEcaCommands
    {
        Task Run<A>(string commandId, A args);
    }
}
