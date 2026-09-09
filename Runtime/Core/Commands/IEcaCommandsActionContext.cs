namespace EcaSystems.Core
{
    public interface IEcaCommandsActionContext : IEcaActionContext
    {
        IEcaCommands Commands { get; }
    }

    public interface IEcaCommandsActionContext<out TEventContext>
        : IEcaCommandsActionContext, IEcaActionContext<TEventContext> { }
}
