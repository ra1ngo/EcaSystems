using System;

namespace EcaSystems.Core
{
    public interface IEcaExecutionActionContext<out TEventContext> : IEcaCommandsActionContext<TEventContext>
    {
        EcaRuleExecutionGroupState RuleExecutionGroupState { get; }
    }

    public sealed class EcaExecutionActionContext<TEventContext>
        : EcaActionContext<TEventContext>, IEcaExecutionActionContext<TEventContext>
    {
        public EcaRuleExecutionGroupState RuleExecutionGroupState { get; }
        public IEcaCommands Commands { get; }

        public EcaExecutionActionContext(TEventContext eventContext,
            EcaRuleExecutionGroupState ruleExecutionGroupState, IEcaCommandRunner commandRunner)
            : base(eventContext)
        {
            RuleExecutionGroupState = ruleExecutionGroupState ?? throw new ArgumentNullException(nameof(ruleExecutionGroupState));
            Commands = (commandRunner ?? throw new ArgumentNullException(nameof(commandRunner))).Bind(this);
        }
    }
}
