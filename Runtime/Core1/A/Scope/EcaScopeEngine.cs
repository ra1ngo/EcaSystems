using System;
using System.Collections.Generic;
using System.Globalization;

namespace EcaSystems.Core1
{
    public sealed class EcaScopeEngine : IDisposable
    {
        private readonly EcaEventRegistry _events;
        private readonly Dictionary<string, EcaScope> _scopes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<EcaScope>> _children = new(StringComparer.Ordinal);
        private long _nextScopeId = 1;
        private bool _isDisposed;

        public EcaScopeEngine(EcaEventRegistry events)
        {
            _events = events ?? throw new ArgumentNullException(nameof(events));
        }

        public int ScopeCount => _scopes.Count;
        public EcaScope CreateScope(string scopeId = null) => CreateScope(null, scopeId);

        public bool TryGetScope(string scopeId, out EcaScope scope)
        {
            scope = null;
            return scopeId != null && _scopes.TryGetValue(scopeId, out scope);
        }

        internal EcaScope CreateScope(EcaScope parent, string scopeId)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(EcaScopeEngine));
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

            /* Общий только каталог Event. Каждая композиция получает собственные
               registries, dispatcher, runner, BaseEngine и lifetime GroupState. */
            var state = new EcaScopeState(scopeId);
            var groups = new EcaRuleExecutionRegistry();
            var runner = new EcaScopeRuleRunner(groups, state);
            var execution = new EcaExecutionEngine(_events, groups, runner);
            var scope = new EcaScope(this, execution, state, parent?.ScopeId);
            _scopes.Add(scopeId, scope);
            _children.Add(scopeId, new HashSet<EcaScope>());
            if (parent != null) _children[parent.ScopeId].Add(scope);
            return scope;
        }

        internal void DisposeScope(EcaScope scope)
        {
            /* Старый объект с переиспользованным ID не может удалить новый Scope. */
            if (!TryGetScope(scope.ScopeId, out var current) || !ReferenceEquals(current, scope)) return;
            foreach (var child in new List<EcaScope>(_children[scope.ScopeId])) child.Dispose();
            _children.Remove(scope.ScopeId);
            if (scope.ParentScopeId != null && _children.TryGetValue(scope.ParentScopeId, out var siblings)) siblings.Remove(scope);
            _scopes.Remove(scope.ScopeId);
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            foreach (var scope in new List<EcaScope>(_scopes.Values))
                if (scope.ParentScopeId == null) scope.Dispose();
        }
    }
}
