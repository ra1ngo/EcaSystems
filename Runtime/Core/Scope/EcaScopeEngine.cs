using System;
using System.Collections.Generic;
using System.Globalization;

namespace EcaSystems.Core
{
    public sealed class EcaScopeEngine : IDisposable
    {
        private readonly Dictionary<string, EcaScope> _scopes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<EcaScope>> _children = new(StringComparer.Ordinal);
        private long _nextScopeId = 1;
        private bool _isDisposed;

        private readonly IEcaCommandRunner _commandRunner;

        public EcaScopeEngine(IEcaCommandRunner commandRunner)
        {
            _commandRunner = commandRunner ?? throw new ArgumentNullException(nameof(commandRunner));
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
            if (parent != null && (parent.IsDisposed ||
                !TryGetScope(parent.ScopeId, out var activeParent) || !ReferenceEquals(activeParent, parent)))
                throw new ObjectDisposedException(nameof(EcaScope));

            if (scopeId == null)
            {
                do
                {
                    scopeId = "scope-" + (_nextScopeId++).ToString(CultureInfo.InvariantCulture);
                } while (_scopes.ContainsKey(scopeId));
            }
            else if (string.IsNullOrWhiteSpace(scopeId))
                throw new ArgumentException("Scope id cannot be empty.", nameof(scopeId));

            if (_scopes.ContainsKey(scopeId))
                throw new InvalidOperationException($"Scope '{scopeId}' is already active.");

            /* Общую Rule можно регистрировать по ссылке, но engines/registries и
               GroupState принадлежат каждому Scope отдельно; общий только CommandRunner. */
            var scope = new EcaScope(this, new EcaExecutionEngine(_commandRunner), scopeId, parent?.ScopeId);
            _scopes.Add(scopeId, scope);
            _children.Add(scopeId, new HashSet<EcaScope>());
            if (parent != null) _children[parent.ScopeId].Add(scope);
            return scope;
        }

        internal void DisposeScope(EcaScope scope)
        {
            // Повторно использованный ID не даёт старому объекту удалить новый Scope.
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
