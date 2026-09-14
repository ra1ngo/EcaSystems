namespace EcaSystems.Core2
{
    public interface IEcaCommandRunner
    {
        IEcaCommands Bind(IEcaActionContext context);
    }
}
