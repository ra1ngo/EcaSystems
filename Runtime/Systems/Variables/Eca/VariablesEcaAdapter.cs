using System;
using EcaSystems.Core2;

namespace EcaSystems.Variables.Eca
{
    /// <summary>Connect exports before adapter.Connect; adapter.Disconnect before disconnecting exports.</summary>
    public sealed class VariablesEcaAdapter
    {
        private readonly EcaVariablesSystem _variables;
        private readonly IEcaEventEmitter _emitter;
        private readonly IEcaEvent<EcaVariableChangedEventState> _changed;
        private readonly EcaVariableChangedEventStateMapper _mapper = new();
        private bool _connected;

        public VariablesEcaAdapter(EcaVariablesSystem variables, IEcaEventRegistry events, IEcaEventEmitter emitter)
        {
            _variables = variables ?? throw new ArgumentNullException(nameof(variables));
            _emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
            if (events == null) throw new ArgumentNullException(nameof(events));
            var id = EcaVariablesEventIds.Get(EcaVariablesEventKey.ECA_EVENT_VARIABLE_CHANGED_ID);
            var declaration = events.Resolve(id);
            if (declaration is not IEcaEvent<EcaVariableChangedEventState> typed ||
                declaration.EventStateType != typeof(EcaVariableChangedEventState))
                throw new ArgumentException($"Event '{id}' must declare IEcaEvent<EcaVariableChangedEventState> and matching metadata.", nameof(events));
            _changed = typed;
        }

        public void Connect()
        {
            if (_connected) throw new InvalidOperationException("Variables adapter is already connected.");
            _variables.VariableChanged += Changed;
            _connected = true;
        }

        public void Disconnect()
        {
            if (!_connected) return;
            _variables.VariableChanged -= Changed;
            _connected = false;
        }

        private void Changed(EcaVariableChanged change) => _emitter.Fire(_changed, _mapper.Map(change));
    }
}
