using System;
using System.Collections.Generic;

using EcaRuleId = System.String;

namespace EcaSystems.Core
{
    public sealed class EcaRuleExecutionRegistry
    {
        private readonly Dictionary<EcaRuleId, EcaRuleExecutionGroup> _groups = new();

        public EcaRuleExecutionGroup GetOrCreate(EcaRuleId ruleId, EcaOverlap overlap)
        {
            if (_groups.TryGetValue(ruleId, out var group))
            {
                if (group.Overlap != overlap)
                {
                    throw new InvalidOperationException(
                        $"Rule execution group '{ruleId}' already exists with overlap " +
                        $"'{group.Overlap}', but '{overlap}' was requested."
                    );
                }

                return group;
            }

            group = new EcaRuleExecutionGroup(ruleId, overlap);

            _groups.Add(ruleId, group);

            return group;
        }

        public EcaRuleExecutionGroup Get(EcaRuleId ruleId)
        {
            if (_groups.TryGetValue(ruleId, out var group))
                return group;

            throw new InvalidOperationException(
                $"Rule execution group '{ruleId}' is not registered."
            );
        }

        public bool TryGet(EcaRuleId ruleId, out EcaRuleExecutionGroup group)
        {
            return _groups.TryGetValue(ruleId, out group);
        }

        public bool Remove(EcaRuleId ruleId)
        {
            return _groups.Remove(ruleId);
        }
    }
}