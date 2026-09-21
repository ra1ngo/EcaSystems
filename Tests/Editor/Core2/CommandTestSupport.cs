using System;
using System.Threading.Tasks;
using EcaSystems.Core2;

namespace EcaSystems.Tests.Core2
{
    internal static class CommandTestSupport
    {
        internal class Context : IEcaActionContext { public int Value { get; set; } }
        internal sealed class DerivedContext : Context { }
        internal class Args { public int Value { get; set; } }
        internal sealed class DerivedArgs : Args { }

        internal sealed class Command<C, A> : AEcaCommand<IEcaRuleState, C, A> where C : IEcaActionContext
        {
            public string CommandId { get; set; } = "command";
            public override string Id => CommandId;
            public Func<C, A, Task> Handler { get; set; } = (context, args) => Task.CompletedTask;
            public override Task Run(IEcaRuleState state, C context, A args) => Handler(context, args);
        }

        internal sealed class ActionInput : IEcaActionContext
        {
            public int Total { get; set; }
        }

        internal sealed class Rule : IEcaRule<int, BaseTestSupport.State>
        {
            public string Id { get; set; } = "rule";
            public string Name => Id;
            public string Description => Id;
            public IEcaEvent<int> Event { get; set; }
            public IEcaCondition<BaseTestSupport.State> Condition { get; set; }
            public IEcaAction<BaseTestSupport.State> Action { get; set; }
            IEcaEvent IEcaRule.Event => Event;
            IEcaCondition IEcaRule.Condition => Condition;
            IEcaAction IEcaRule.Action => Action;
        }

        internal sealed class CommandAction : IEcaAction<BaseTestSupport.State>
        {
            public string Id => "action";
            public string Name => Id;
            public string Description => Id;
            public Func<BaseTestSupport.State, ActionInput, Task> Handler { get; set; }
            public Task Run(BaseTestSupport.State state, IEcaActionContext context) => Handler(state, (ActionInput)context);
        }
    }
}
