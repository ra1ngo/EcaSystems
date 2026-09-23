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

        public EcaScopeRuntime(IEcaEventRegistry events, IEcaConditionChecker conditionChecker, IEcaActionRunner actionRunner)
        {
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

        internal EcaScope CreateScope(EcaScope parent, string scopeId)
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
            return scope;
        }

        internal void DisposeScope(EcaScope scope)
        {
            // Старый экземпляр не может удалить replacement с тем же ScopeId.
            if (!TryGetScope(scope.ScopeId, out var active) || !ReferenceEquals(active, scope)) return;
            foreach (var child in new List<EcaScope>(_children[scope.ScopeId])) child.Dispose();
            _children.Remove(scope.ScopeId);
            if (scope.ParentScopeId != null && _children.TryGetValue(scope.ParentScopeId, out var siblings))
                siblings.Remove(scope);
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
