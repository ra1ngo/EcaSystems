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

        public IEcaExecutionGroup<E, R, C, A> Get<E, R, C, A>(string ruleId)
            where R : IEcaExecutionRuleState<E>
            where C : IEcaExecutionConditionContext
            where A : IEcaExecutionActionContext
        {
            var group = Get(ruleId);
            if (group is IEcaExecutionGroup<E, R, C, A> typedGroup) return typedGroup;
            throw new InvalidOperationException($"Execution group '{ruleId}' is incompatible with requested runtime types.");
        }

        public bool TryGet<E, R, C, A>(string ruleId, out IEcaExecutionGroup<E, R, C, A> group)
            where R : IEcaExecutionRuleState<E>
            where C : IEcaExecutionConditionContext
            where A : IEcaExecutionActionContext
        {
            group = null;
            if (!TryGet(ruleId, out var registered)) return false;
            group = registered as IEcaExecutionGroup<E, R, C, A>;
            return group != null;
        }

        private static void ValidateId(string ruleId)
        {
            if (string.IsNullOrWhiteSpace(ruleId)) throw new ArgumentException("Rule id cannot be empty.", nameof(ruleId));
        }
    }
}
