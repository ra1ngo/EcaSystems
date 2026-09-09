namespace EcaSystems.Core
{
    public interface IEcaCommandRunner
    {
        IEcaCommands Bind(IEcaActionContext context);
    }
}
