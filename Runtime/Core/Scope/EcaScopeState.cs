using System;

namespace EcaSystems.Core
{
    /* Данные конкретного Scope, без ссылки на владельца или runtime-сервисы.
       Повторное использование ScopeId создаёт новый экземпляр состояния. */
    public sealed class EcaScopeState
    {
        public string ScopeId { get; }

        public EcaScopeState(string scopeId)
        {
            if (string.IsNullOrWhiteSpace(scopeId))
                throw new ArgumentException("Scope id cannot be empty.", nameof(scopeId));
            ScopeId = scopeId;
        }
    }
}
