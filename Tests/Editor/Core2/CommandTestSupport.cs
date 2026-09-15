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

        internal sealed class Command<C, A> : IEcaCommand<C, A> where C : IEcaActionContext
        {
            public string Id { get; set; } = "command";
            public Func<C, A, Task> Handler { get; set; } = (context, args) => Task.CompletedTask;
            public Task Run(C context, A args) => Handler(context, args);
        }

        internal sealed class CommandsContext : IEcaCommandsActionContext
        {
            public IEcaCommands Commands { get; }
            public int Total { get; set; }
            public CommandsContext(IEcaCommandRunner runner) => Commands = runner.Bind(this);
        }

        internal sealed class Rule : IEcaRule<int, BaseTestSupport.State, BaseTestSupport.ConditionContext, CommandsContext>
        {
            public string Id { get; set; } = "rule";
            public string Name => Id;
            public string Description => Id;
            public IEcaEvent<int> Event { get; set; }
            public IEcaCondition<BaseTestSupport.State, BaseTestSupport.ConditionContext> Condition { get; set; }
            public IEcaAction<BaseTestSupport.State, CommandsContext> Action { get; set; }
            IEcaEvent IEcaRule.Event => Event;
            IEcaCondition IEcaRule.Condition => Condition;
            IEcaAction IEcaRule.Action => Action;
        }

        internal sealed class CommandAction : IEcaAction<BaseTestSupport.State, CommandsContext>
        {
            public string Id => "action";
            public string Name => Id;
            public string Description => Id;
            public Func<BaseTestSupport.State, CommandsContext, Task> Handler { get; set; }
            public Task Run(BaseTestSupport.State state, CommandsContext context) => Handler(state, context);
        }
    }
}
