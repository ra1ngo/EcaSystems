using System;

namespace EcaSystems.Core2
{
    public sealed class EcaStateResolver : IEcaStateResolver
    {
        private readonly EcaStateRegistry _registry;
        public EcaStateResolver(EcaStateRegistry registry) =>
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));

        public T Resolve<T>(string stateId, IEcaRuleState ruleState)
        {
            var resolve = _registry.Resolve<T>(stateId);
            if (ruleState == null) throw new ArgumentNullException(nameof(ruleState));
            return resolve(ruleState);
        }

        public T Resolve<T>(string stateId, IEcaRuleState ruleState, object payload)
        {
            var resolve = _registry.ResolveWithPayload<T>(stateId);
            if (ruleState == null) throw new ArgumentNullException(nameof(ruleState));
            return resolve(ruleState, payload);
        }
    }
}
