using System;
using System.Collections.Generic;

namespace EcaSystems.Core2
{
    // Stores access functions, never the external state returned by them.
    public sealed class EcaStateRegistry
    {
        private readonly Dictionary<Type, Delegate> _registrations = new();
        internal IEnumerable<KeyValuePair<Type, Delegate>> Registrations => _registrations;

        public void Register<T>(Func<IEcaRuleState, T> resolve)
        {
            if (resolve == null) throw new ArgumentNullException(nameof(resolve));
            Register(typeof(T), resolve);
        }

        public bool Contains<T>() => Contains(typeof(T));
        public bool Unregister<T>() => Unregister(typeof(T));

        internal void Register(Type type, Delegate resolve)
        {
            if (_registrations.ContainsKey(type))
                throw new InvalidOperationException($"State '{type}' is already registered.");
            _registrations.Add(type, resolve);
        }

        internal bool Contains(Type type) => _registrations.ContainsKey(type);
        internal bool Unregister(Type type) => _registrations.Remove(type);
        internal bool CheckRegistered(Type type, Delegate resolve) =>
            _registrations.TryGetValue(type, out var current) && ReferenceEquals(current, resolve);

        internal Func<IEcaRuleState, T> Resolve<T>()
        {
            if (_registrations.TryGetValue(typeof(T), out var resolve))
                return (Func<IEcaRuleState, T>)resolve;
            throw new InvalidOperationException($"State '{typeof(T)}' is not registered.");
        }
    }
}
