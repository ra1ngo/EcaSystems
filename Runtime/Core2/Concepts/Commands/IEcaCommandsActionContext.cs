namespace EcaSystems.Core2
{
    public interface IEcaCommandsActionContext : IEcaActionContext
    {
        IEcaCommands Commands { get; }
    }
}