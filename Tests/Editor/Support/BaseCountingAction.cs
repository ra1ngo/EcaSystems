using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EcaSystems.Core;

namespace EcaSystems.Tests.Support
{
    internal sealed class BaseCountingAction<TEventContext>
        : IEcaAction<EcaActionContext<TEventContext>>
    {
        public int RunCount { get; private set; }
        public int LastValue { get; private set; }

        public Task Run(
            EcaActionContext<TEventContext> context)
        {
            RunCount++;

            if (context.EventContext
                is TestEventContext testContext)
            {
                LastValue = testContext.Value;
            }

            return Task.CompletedTask;
        }
    }
}
