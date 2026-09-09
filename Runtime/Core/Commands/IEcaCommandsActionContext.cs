namespace EcaSystems.Core
{
    public interface IEcaCommandsActionContext : IEcaActionContext
    {
        IEcaCommands Commands { get; }
    }

    public interface IEcaCommandsActionContext<TEventContext>
        : IEcaCommandsActionContext, IEcaActionContext<TEventContext> { }
}
