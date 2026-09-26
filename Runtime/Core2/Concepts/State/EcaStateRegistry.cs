using System;
using System.Collections.Generic;

namespace EcaSystems.Core2
{
    // Stores access functions, never the external state returned by them.
    public sealed class EcaStateRegistry
    {
        // Frozen registration IDs; resolver records remain internal.
        public IReadOnlyList<string> GetSnapshot() => new List<string>(_registrations.Keys).AsReadOnly();

        internal sealed class Registration
        {
            internal string Id { get; }
            internal Type StateType { get; }
            internal Delegate Resolver { get; }
            internal bool HasPayload { get; }
            internal Registration(string id, Type stateType, Delegate resolver, bool hasPayload)
            {
                Id = id;
                StateType = stateType;
                Resolver = resolver;
                HasPayload = hasPayload;
            }
        }

        private readonly Dictionary<string, Registration> _registrations = new(StringComparer.Ordinal);
        internal IEnumerable<Registration> Registrations => _registrations.Values;

        public void Register<T>(string id, Func<IEcaRuleState, T> resolve)
        {
            ValidateId(id);
            if (resolve == null) throw new ArgumentNullException(nameof(resolve));
            Register(new Registration(id, typeof(T), resolve, false));
        }

        public void Register<T>(string id, Func<IEcaRuleState, object, T> resolve)
        {
            ValidateId(id);
            if (resolve == null) throw new ArgumentNullException(nameof(resolve));
            Register(new Registration(id, typeof(T), resolve, true));
        }

        public bool Contains(string id)
        {
            ValidateId(id);
            return _registrations.ContainsKey(id);
        }

        public bool Unregister(string id)
        {
            ValidateId(id);
            return _registrations.Remove(id);
        }

        internal void Register(Registration registration)
        {
            if (_registrations.ContainsKey(registration.Id))
                throw new InvalidOperationException($"State '{registration.Id}' is already registered.");
            _registrations.Add(registration.Id, registration);
        }

        internal bool CheckRegistered(Registration registration) =>
            _registrations.TryGetValue(registration.Id, out var current) &&
            current.StateType == registration.StateType && current.HasPayload == registration.HasPayload &&
            ReferenceEquals(current.Resolver, registration.Resolver);

        internal Func<IEcaRuleState, T> Resolve<T>(string id) =>
            (Func<IEcaRuleState, T>)ResolveRegistration<T>(id, false).Resolver;

        internal Func<IEcaRuleState, object, T> ResolveWithPayload<T>(string id) =>
            (Func<IEcaRuleState, object, T>)ResolveRegistration<T>(id, true).Resolver;

        private Registration ResolveRegistration<T>(string id, bool hasPayload)
        {
            ValidateId(id);
            if (!_registrations.TryGetValue(id, out var registration))
                throw new InvalidOperationException($"State '{id}' is not registered.");
            if (registration.StateType != typeof(T))
                throw new InvalidOperationException($"State '{id}' declares '{registration.StateType}', not '{typeof(T)}'.");
            if (registration.HasPayload != hasPayload)
                throw new InvalidOperationException($"State '{id}' resolver shape does not match the requested overload.");
            return registration;
        }

        private static void ValidateId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("State id cannot be empty.", nameof(id));
        }
    }
}
