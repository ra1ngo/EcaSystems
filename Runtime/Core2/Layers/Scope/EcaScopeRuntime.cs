using System;
using System.Collections.Generic;
using System.Globalization;

namespace EcaSystems.Core2
{
    public sealed class EcaScopeRuntime : IDisposable
    {
        private readonly IEcaEventRegistry _events;
        private readonly IEcaConditionChecker _conditionChecker;
        private readonly IEcaActionRunner _actionRunner;
        private readonly Dictionary<string, EcaScope> _scopes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<EcaScope>> _children = new(StringComparer.Ordinal);
        private long _nextScopeId = 1;
        private bool _isDisposed;
        private bool _mutating;
        internal EcaScopeLifecycle Lifecycle { get; }

        public EcaScopeRuntime(IEcaEventRegistry events, IEcaConditionChecker conditionChecker, IEcaActionRunner actionRunner, EcaScopeLifecycle lifecycle = null)
        {
            Lifecycle = lifecycle ?? new EcaScopeLifecycle();
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _conditionChecker = conditionChecker ?? throw new ArgumentNullException(nameof(conditionChecker));
            _actionRunner = actionRunner ?? throw new ArgumentNullException(nameof(actionRunner));
        }

        public int ScopeCount => _scopes.Count;
        public EcaScope CreateScope(string scopeId = null) => CreateScope(null, scopeId);

        public bool TryGetScope(string scopeId, out EcaScope scope)
        {
            scope = null;
            return scopeId != null && _scopes.TryGetValue(scopeId, out scope);
        }

        internal void Mutate(Action operation)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(EcaScopeRuntime));
            if (_mutating) throw new InvalidOperationException("Topology cannot change during lifecycle mutation.");
            _mutating = true;
            try { operation(); }
            finally { _mutating = false; }
        }

        internal EcaScope CreateScope(EcaScope parent, string scopeId)
        {
            EcaScope created = null;
            Mutate(() => created = CreateScopeCore(parent, scopeId));
            return created;
        }

        private EcaScope CreateScopeCore(EcaScope parent, string scopeId)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(EcaScopeRuntime));
            if (parent != null && (parent.IsDisposed || !TryGetScope(parent.ScopeId, out var activeParent) ||
                !ReferenceEquals(activeParent, parent)))
                throw new ObjectDisposedException(nameof(EcaScope));

            if (scopeId == null)
            {
                do { scopeId = "scope-" + (_nextScopeId++).ToString(CultureInfo.InvariantCulture); }
                while (_scopes.ContainsKey(scopeId));
            }
            else if (string.IsNullOrWhiteSpace(scopeId))
                throw new ArgumentException("Scope id cannot be empty.", nameof(scopeId));
            if (_scopes.ContainsKey(scopeId)) throw new InvalidOperationException($"Scope '{scopeId}' is already active.");

            var scope = new EcaScope(this, new EcaScopeState(scopeId),
                new EcaBaseRuleRegistry(_events), new EcaExecutionGroupRegistry(), _conditionChecker, _actionRunner, parent?.ScopeId);
            _scopes.Add(scopeId, scope);
            _children.Add(scopeId, new HashSet<EcaScope>());
            if (parent != null) _children[parent.ScopeId].Add(scope);
            try { Lifecycle.ConnectScope(scope); }
            catch { RemoveCore(scope); throw; }
            return scope;
        }

        // Deterministic parent-first snapshot, built from the authoritative topology.
        internal IReadOnlyList<EcaScope> GetSnapshot()
        {
            var result = new List<EcaScope>();
            var roots = new List<EcaScope>();
            foreach (var scope in _scopes.Values) if (scope.ParentScopeId == null) roots.Add(scope);
            roots.Sort((a, b) => StringComparer.Ordinal.Compare(a.ScopeId, b.ScopeId));
            foreach (var root in roots) AppendSubtree(root, result);
            return result.AsReadOnly();
        }

        private void AppendSubtree(EcaScope scope, List<EcaScope> result)
        {
            result.Add(scope);
            var children = new List<EcaScope>(_children[scope.ScopeId]);
            children.Sort((a, b) => StringComparer.Ordinal.Compare(a.ScopeId, b.ScopeId));
            foreach (var child in children) AppendSubtree(child, result);
        }

        internal void DisposeScope(EcaScope scope)
        {
            if (!TryGetScope(scope.ScopeId, out var active) || !ReferenceEquals(active, scope)) return;
            Mutate(() =>
            {
                var subtree = new List<EcaScope>();
                AppendSubtree(scope, subtree);
                PrepareAndCommit(subtree);
            });
        }

        private void PrepareAndCommit(List<EcaScope> parentFirst)
        {
            parentFirst.Reverse();
            EcaScopeLifecycle.Apply(parentFirst, Lifecycle.DisconnectScope, Lifecycle.RestoreScope);
            // No user callbacks after prepare: commit cannot trigger lifecycle failure.
            foreach (var scope in parentFirst) RemoveCore(scope);
        }

        private void RemoveCore(EcaScope scope)
        {
            _children.Remove(scope.ScopeId);
            if (scope.ParentScopeId != null && _children.TryGetValue(scope.ParentScopeId, out var siblings))
                siblings.Remove(scope);
            _scopes.Remove(scope.ScopeId);
            scope.CommitDispose();
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            Mutate(() => PrepareAndCommit(new List<EcaScope>(GetSnapshot())));
            _isDisposed = true;
        }
    }
}
