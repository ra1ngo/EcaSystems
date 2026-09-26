using System;
using System.Collections.Generic;
using EcaSystems.Core2;

namespace EcaSystems.Variables.Eca
{
    // Only active external bindings live here. Core registries remain the source of Scope/Rule membership.
    internal sealed class EcaVariablesLifecycleConnector : IEcaSystemLifecycleConnector
    {
        private sealed class Binding
        {
            internal EcaScope Scope;
            internal string Path;
            internal EcaVariableStore Store;
            internal VariablesEcaAdapter Adapter;
        }

        private readonly EcaVariablesSystem _variables;
        private readonly IEcaEventRegistry _events;
        private readonly Dictionary<EcaScopeState, Binding> _bindings = new();

        internal EcaVariablesLifecycleConnector(EcaVariablesSystem variables, IEcaEventRegistry events)
        {
            _variables = variables;
            _events = events;
        }

        public void ConnectScope(EcaScope scope)
        {
            if (scope == null) throw new ArgumentNullException(nameof(scope));
            if (scope.IsDisposed) throw new ObjectDisposedException(nameof(EcaScope));
            if (_bindings.ContainsKey(scope.State)) throw new InvalidOperationException("Scope is already bound.");
            Binding parent = null;
            if (scope.ParentScopeId != null)
            {
                foreach (var candidate in _bindings.Values)
                    if (candidate.Scope.ScopeId == scope.ParentScopeId) { parent = candidate; break; }
                if (parent == null) throw new InvalidOperationException("Parent Scope must be bound first.");
            }
            var path = (parent == null ? "" : parent.Path + "/") + Uri.EscapeDataString(scope.ScopeId);
            var store = EnsureStore("scope:" + path, parent?.Store.Id);
            var adapter = new VariablesEcaAdapter(store, _events, scope.EventEmitter);
            var binding = new Binding { Scope = scope, Path = path, Store = store, Adapter = adapter };
            _bindings.Add(scope.State, binding);
            try { adapter.Connect(); }
            catch { _bindings.Remove(scope.State); throw; }
        }

        public void DisconnectScope(EcaScope scope)
        {
            var binding = GetBinding(scope);
            binding.Adapter.Disconnect();
            _bindings.Remove(scope.State);
            // Persistent Stores and values are deliberately not removed.
        }

        public void ConnectRule(EcaScope scope, IEcaRule rule)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            var binding = GetBinding(scope);
            if (!scope.TryGetGroup(rule.Id, out var group) || !ReferenceEquals(group.Rule, rule))
                throw new InvalidOperationException("Rule is not the registered instance.");
            EnsureStore(RuleStoreId(binding, rule.Id), binding.Store.Id);
        }

        public void DisconnectRule(EcaScope scope, IEcaRule rule)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            GetBinding(scope);
            // No Rule subscriptions or duplicate membership collection to remove.
            // Core membership ends after this callback; the Rule Store is persistent.
        }

        internal EcaVariableStore ResolveRuleStore(IEcaScopeRuleState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.ScopeState == null || !_bindings.TryGetValue(state.ScopeState, out var binding) ||
                string.IsNullOrWhiteSpace(state.RuleId) || !binding.Scope.TryGetGroup(state.RuleId, out _))
                throw new InvalidOperationException("Rule state has no current Variables binding.");
            return _variables.GetStore(RuleStoreId(binding, state.RuleId));
        }

        private static string RuleStoreId(Binding binding, string ruleId) =>
            "rule:" + binding.Path + ":" + Uri.EscapeDataString(ruleId);

        private Binding GetBinding(EcaScope scope)
        {
            if (scope == null) throw new ArgumentNullException(nameof(scope));
            if (_bindings.TryGetValue(scope.State, out var binding) && ReferenceEquals(binding.Scope, scope)) return binding;
            throw new InvalidOperationException("Scope is not bound.");
        }

        private EcaVariableStore EnsureStore(string id, string parentId)
        {
            if (!_variables.TryGetStore(id, out var store)) return _variables.CreateStore(id, parentId);
            if (store.ParentId != parentId) throw new InvalidOperationException($"Persistent Store '{id}' has a different parent.");
            return store;
        }
    }
}
