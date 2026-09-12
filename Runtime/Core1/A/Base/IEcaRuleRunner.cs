using System.Threading.Tasks;

namespace EcaSystems.Core1
{
    public interface IEcaRuleRunner
    {
        void ValidateRule<TEventState>(IEcaRule rule);
        IEcaRuleRun CreateRun(IEcaRule rule, EcaEventOccurrence occurrence);
        bool Check(IEcaRuleRun run);
        Task RunAction(IEcaRuleRun run);
    }
}
