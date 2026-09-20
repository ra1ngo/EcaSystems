using System;
using System.Collections.Generic;

namespace EcaSystems.Core2
{
    public sealed class EcaExecutionGroupRegistry : IEcaExecutionGroupRegistry
    {
        private readonly Dictionary<string, IEcaExecutionGroup> _groups = new(StringComparer.Ordinal);

        public void Register(IEcaExecutionGroup group)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));
            ValidateId(group.RuleId);
            if (_groups.ContainsKey(group.RuleId))
                throw new InvalidOperationException($"Execution group '{group.RuleId}' is already registered.");
            _groups.Add(group.RuleId, group);
        }

        public bool Unregister(string ruleId)
        {
            ValidateId(ruleId);
            return _groups.Remove(ruleId);
        }

        public IEcaExecutionGroup Get(string ruleId)
        {
            if (TryGet(ruleId, out var group)) return group;
            throw new InvalidOperationException($"Execution group '{ruleId}' is not registered.");
        }

        public bool TryGet(string ruleId, out IEcaExecutionGroup group)
        {
            ValidateId(ruleId);
            return _groups.TryGetValue(ruleId, out group);
        }

        public IEcaExecutionGroup<E> Get<E>(string ruleId)
        {
            var group = Get(ruleId);
            if (group is IEcaExecutionGroup<E> typedGroup) return typedGroup;
            throw new InvalidOperationException($"Execution group '{ruleId}' is incompatible with requested event type.");
        }

        public bool TryGet<E>(string ruleId, out IEcaExecutionGroup<E> group)
        {
            group = null;
            if (!TryGet(ruleId, out var registered)) return false;
            group = registered as IEcaExecutionGroup<E>;
            return group != null;
        }

        public IEcaExecutionGroup<E, R> Get<E, R>(string ruleId)
            where R : IEcaExecutionRuleState<E>
        {
            var group = Get(ruleId);
            if (group is IEcaExecutionGroup<E, R> typedGroup) return typedGroup;
            throw new InvalidOperationException($"Execution group '{ruleId}' is incompatible with requested runtime types.");
        }

        public bool TryGet<E, R>(string ruleId, out IEcaExecutionGroup<E, R> group)
            where R : IEcaExecutionRuleState<E>
        {
            group = null;
            if (!TryGet(ruleId, out var registered)) return false;
            group = registered as IEcaExecutionGroup<E, R>;
            return group != null;
        }

        private static void ValidateId(string ruleId)
        {
            if (string.IsNullOrWhiteSpace(ruleId)) throw new ArgumentException("Rule id cannot be empty.", nameof(ruleId));
        }
    }
}
