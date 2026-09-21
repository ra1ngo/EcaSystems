using System;
using System.Threading.Tasks;

namespace EcaSystems.Core2
{
    public abstract class AEcaAction<R> : IEcaAction<R> where R : IEcaRuleState
    {
        private IEcaCommands _commands;
        private IEcaStateResolver _state;
        protected IEcaStateResolver State => _state
            ?? throw new InvalidOperationException("Action has not been initialized with StateResolver.");
        protected IEcaCommands Commands => _commands
            ?? throw new InvalidOperationException("Action has not been initialized with Commands.");

        public abstract string Id { get; }
        public virtual string Name => Id;
        public virtual string Description => null;
        public abstract Task Run(R state, IEcaActionContext context);

        internal void Initialize(IEcaCommands commands, IEcaStateResolver stateResolver)
        {
            if (commands == null) throw new ArgumentNullException(nameof(commands));
            if (stateResolver == null) throw new ArgumentNullException(nameof(stateResolver));
            if (_commands != null) throw new InvalidOperationException("Action is already initialized.");
            _commands = commands;
            _state = stateResolver;
        }
    }
}
