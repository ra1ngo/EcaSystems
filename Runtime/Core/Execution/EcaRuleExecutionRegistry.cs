using System;
using System.Collections.Generic;
using EcaRuleId = System.String;

namespace EcaSystems.Core
{
    public sealed class EcaRuleExecutionRegistry : IEcaRuleExecutionRegistry
    {
        private readonly Dictionary<EcaRuleId, IEcaRuleExecutionGroup> _groups = new();

        public void Register<TEventContext>(IEcaRule<TEventContext, IEcaExecutionConditionContext<TEventContext>, IEcaExecutionActionContext<TEventContext>> rule,
            EcaRunMode runMode, IEcaRuleRunner ruleRunner)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            if (runMode == null) throw new ArgumentNullException(nameof(runMode));
            if (ruleRunner == null) throw new ArgumentNullException(nameof(ruleRunner));
            if (TryGet<TEventContext>(rule.Id, out var existing))
            {
                if (existing.RunMode.Overlap != runMode.Overlap || existing.RunMode.Limit != runMode.Limit)
                    throw new InvalidOperationException($"Rule execution group '{rule.Id}' already exists with a different run mode.");
                // Preserve re-registration of the same rule without rebinding active executions.
                if (!ReferenceEquals(existing.Rule, rule))
                    throw new InvalidOperationException($"Rule execution group '{rule.Id}' already belongs to another rule.");
                return;
            }

            _groups.Add(rule.Id, new EcaRuleExecutionGroup<TEventContext>(rule, runMode, ruleRunner));
        }

        public EcaRuleExecutionGroup<TEventContext> Get<TEventContext>(EcaRuleId ruleId)
        {
            if (TryGet<TEventContext>(ruleId, out var group)) return group;
            throw new InvalidOperationException($"Rule execution group '{ruleId}' is not registered.");
        }

        public bool TryGet<TEventContext>(EcaRuleId ruleId,
            out EcaRuleExecutionGroup<TEventContext> group)
        {
            if (!_groups.TryGetValue(ruleId, out var stored))
            {
                group = null;
                return false;
            }
            if (!(stored is EcaRuleExecutionGroup<TEventContext> typed))
                throw new InvalidOperationException($"Rule execution group '{ruleId}' is incompatible with event context '{typeof(TEventContext)}'.");
            group = typed;
            return true;
        }

        // Storage operations only; do not infer lifecycle policy.
        public bool Remove(EcaRuleId ruleId) => _groups.Remove(ruleId);
        public void Clear() => _groups.Clear();
    }
}
