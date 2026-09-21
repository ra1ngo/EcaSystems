using System;

namespace EcaSystems.Core2
{
    public sealed class EcaStateResolver : IEcaStateResolver
    {
        private readonly EcaStateRegistry _registry;
        public EcaStateResolver(EcaStateRegistry registry) =>
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));

        public T Resolve<T>(IEcaRuleState ruleState)
        {
            if (ruleState == null) throw new ArgumentNullException(nameof(ruleState));
            return _registry.Resolve<T>()(ruleState);
        }
    }
}
