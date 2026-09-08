using System;
using System.Threading.Tasks;

namespace EcaSystems.Core
{
    public sealed class EcaRuleRunner : IEcaRuleRunner
    {
        public Task Run<TContext>(IEcaRule<TContext> rule, TContext context)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));

            return rule.Action.Run(context);
        }
    }
}
