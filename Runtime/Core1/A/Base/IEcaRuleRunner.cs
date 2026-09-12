using System.Threading.Tasks;

namespace EcaSystems.Core1
{
    /* Opaque per-pipeline data token. No execution methods or lifecycle state. */
    public interface IEcaRuleRun { }

    public interface IEcaRuleRunner
    {
        IEcaRuleRun CreateRun(IEcaRule rule, EcaEventOccurrence occurrence);
        bool Check(IEcaRuleRun run);
        Task RunAction(IEcaRuleRun run);
    }
}
