using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EcaSystems.Core;

namespace EcaSystems.Tests.Support
{
    internal readonly struct ExecutionRecord
    {
        public long Started { get; }
        public long Finished { get; }

        public ExecutionRecord(
            long started,
            long finished)
        {
            Started = started;
            Finished = finished;
        }
    }
}
