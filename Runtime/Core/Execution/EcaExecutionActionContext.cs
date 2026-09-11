using System;

namespace EcaSystems.Core
{
    public interface IEcaExecutionActionContext<TEventContext>
        : IEcaExecutionContext<TEventContext>, IEcaCommandsActionContext<TEventContext>
    {
    }

    public sealed class EcaExecutionActionContext<TEventContext>
        : EcaActionContext<TEventContext>, IEcaExecutionActionContext<TEventContext>
    {
        private IEcaCommands _commands;

        public EcaRuleExecutionGroupState RuleExecutionGroupState { get; }
        public IEcaCommands Commands => _commands
            ?? throw new InvalidOperationException("Commands are not bound to this action context.");

        internal EcaExecutionActionContext(TEventContext eventContext,
            EcaRuleExecutionGroupState ruleExecutionGroupState)
            : base(eventContext)
        {
            RuleExecutionGroupState = ruleExecutionGroupState ?? throw new ArgumentNullException(nameof(ruleExecutionGroupState));
        }

        internal void BindCommands(IEcaCommands commands)
        {
            if (commands == null) throw new ArgumentNullException(nameof(commands));
            if (_commands != null)
                throw new InvalidOperationException("Commands are already bound to this action context.");
            _commands = commands;
        }
    }
}
