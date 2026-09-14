using System.Threading.Tasks;

namespace EcaSystems.Core2
{
    public interface IEcaCommands
    {
        Task Run<TArgs>(string commandId, TArgs args);
    }
}
