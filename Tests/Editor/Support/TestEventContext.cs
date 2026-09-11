using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EcaSystems.Core;

namespace EcaSystems.Tests.Support
{
    internal readonly struct TestEventContext
    {
        public int Value { get; }

        public TestEventContext(int value)
        {
            Value = value;
        }
    }
}
