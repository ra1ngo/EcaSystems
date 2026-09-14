namespace EcaSystems.Core2
{
    public interface IEcaCommandsActionContext : IEcaActionContext
    {
        IEcaCommands Commands { get; }
    }

    public interface IEcaCommandsActionContext<TEventContext>
        : IEcaCommandsActionContext, IEcaActionContext<TEventContext> { }
}
