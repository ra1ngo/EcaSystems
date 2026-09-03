using System;

namespace EcaSystems.Core
{
    public interface IEcaRuntimeContext<out TEventContext> : IEcaContext<TEventContext>
    {
        EcaRuleRuntimeState RuleRuntimeState { get; }
    }

    public sealed class EcaRuntimeContext<TEventContext>
        : EcaContext<TEventContext>,
          IEcaRuntimeContext<TEventContext>
    {
        public EcaRuleRuntimeState RuleRuntimeState { get; }

        public EcaRuntimeContext(
            TEventContext eventContext,
            EcaRuleRuntimeState ruleRuntimeState)
            : base(eventContext)
        {
            RuleRuntimeState = ruleRuntimeState
                ?? throw new ArgumentNullException(nameof(ruleRuntimeState));
        }
    }
}