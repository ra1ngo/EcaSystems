using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EcaSystems.Core;

namespace EcaSystems.Tests.Support
{
    internal sealed class TestCommand<TContext, TArgs> : IEcaCommand<TContext, TArgs>
        where TContext : IEcaActionContext
    {
        private readonly Func<TContext, TArgs, Task> _run;
        public string Id { get; }
        public TestCommand(string id, Func<TContext, TArgs, Task> run) { Id = id; _run = run; }
        public Task Run(TContext context, TArgs args) => _run(context, args);
    }
}
