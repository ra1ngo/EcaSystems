using System;

namespace EcaSystems.Core1
{
    public sealed class EcaScopeState
    {
        public string ScopeId { get; }

        public EcaScopeState(string scopeId)
        {
            if (string.IsNullOrWhiteSpace(scopeId)) throw new ArgumentException("Scope id cannot be empty.", nameof(scopeId));
            ScopeId = scopeId;
        }
    }
}
