using System;
using System.Collections.Generic;
using EcaRuleId = System.String;

namespace EcaSystems.Core
{
    public sealed class EcaRuleRuntimeRegistry
    {
        private readonly Dictionary<EcaRuleId, EcaRuleRuntime> _runtimes = new();

        public EcaRuleRuntime GetOrCreate(EcaRuleId ruleId, EcaOverlap overlap)
        {
            if (_runtimes.TryGetValue(ruleId, out var runtime))
            {
                if (runtime.Overlap != overlap)
                {
                    throw new InvalidOperationException(
                        $"Rule runtime '{ruleId}' already exists with overlap " +
                        $"'{runtime.Overlap}', but '{overlap}' was requested."
                    );
                }

                return runtime;
            }

            runtime = new EcaRuleRuntime(ruleId, overlap);
            _runtimes.Add(ruleId, runtime);

            return runtime;
        }

        public EcaRuleRuntime Get(EcaRuleId ruleId)
        {
            if (_runtimes.TryGetValue(ruleId, out var runtime))
                return runtime;

            throw new InvalidOperationException(
                $"Rule runtime '{ruleId}' is not registered."
            );
        }

        public bool TryGet(EcaRuleId ruleId, out EcaRuleRuntime runtime)
        {
            return _runtimes.TryGetValue(ruleId, out runtime);
        }

        public bool Remove(EcaRuleId ruleId)
        {
            return _runtimes.Remove(ruleId);
        }
    }
}