using System;
using System.Collections.Generic;

namespace EcaSystems.Variables
{
    public sealed class EcaVariableRegistry
    {
        private readonly Dictionary<string, EcaVariable> _variables = new(StringComparer.Ordinal);
        public IReadOnlyCollection<EcaVariable> Variables => _variables.Values;

        public bool Contains(string variableId)
        {
            ValidateId(variableId);
            return _variables.ContainsKey(variableId);
        }

        public EcaVariable Resolve(string variableId)
        {
            ValidateId(variableId);
            if (!_variables.TryGetValue(variableId, out var variable))
                throw new InvalidOperationException($"Variable '{variableId}' is not declared.");
            return variable;
        }

        public bool TryResolve(string variableId, out EcaVariable variable)
        {
            ValidateId(variableId);
            return _variables.TryGetValue(variableId, out variable);
        }

        internal void Register(EcaVariable variable)
        {
            if (variable == null) throw new ArgumentNullException(nameof(variable));
            var variableId = variable.Definition.Id;
            ValidateId(variableId);
            if (_variables.ContainsKey(variableId))
                throw new InvalidOperationException($"Variable '{variableId}' is already declared.");
            _variables.Add(variableId, variable);
        }

        internal static void ValidateId(string variableId)
        {
            if (string.IsNullOrWhiteSpace(variableId))
                throw new ArgumentException("Variable id cannot be empty.", nameof(variableId));
        }
    }
}
