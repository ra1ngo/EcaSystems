using System;
using System.Collections.Generic;

namespace EcaSystems.Core1
{
    public sealed class EcaRuleExecutionRegistry
    {
        private readonly Dictionary<string, EcaRuleExecutionGroup> _groups = new(StringComparer.Ordinal);

        public void Register(IEcaRule rule, EcaExecutionMode mode)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            if (mode == null) throw new ArgumentNullException(nameof(mode));
            if (string.IsNullOrWhiteSpace(rule.Id)) throw new ArgumentException("Rule id cannot be empty.", nameof(rule));
            if (_groups.TryGetValue(rule.Id, out var existing))
            {
                if (!ReferenceEquals(existing.Rule, rule) || existing.ExecutionMode.Limit != mode.Limit ||
                    existing.ExecutionMode.Overlap != mode.Overlap)
                    throw new InvalidOperationException($"Execution group '{rule.Id}' already belongs to another registration.");
                return;
            }
            _groups.Add(rule.Id, new EcaRuleExecutionGroup(rule, mode));
        }

        public EcaRuleExecutionGroup Get(string ruleId)
        {
            if (TryGet(ruleId, out var group)) return group;
            throw new InvalidOperationException($"Execution group '{ruleId}' is not registered.");
        }

        public bool TryGet(string ruleId, out EcaRuleExecutionGroup group)
        {
            group = null;
            return ruleId != null && _groups.TryGetValue(ruleId, out group);
        }

        public bool Remove(string ruleId) => ruleId != null && _groups.Remove(ruleId);
        public void Clear() => _groups.Clear();
    }
}
