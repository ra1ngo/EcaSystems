using System.Threading.Tasks;

namespace EcaSystems.Core
{
    public interface IEcaCommands
    {
        Task Run<TArgs>(string commandId, TArgs args);
    }
}
