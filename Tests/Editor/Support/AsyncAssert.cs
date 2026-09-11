using System;
using System.Diagnostics;
using System.Threading.Tasks;
using NUnit.Framework;

namespace EcaSystems.Tests.Support
{
    internal static class AsyncAssert
    {
        // Core Tasks do not require frames. Keep Unity's synchronization context,
        // yield to continuations and fail rather than hanging on a regression.
        internal static async Task WaitUntil(Func<bool> condition)
        {
            var elapsed = Stopwatch.StartNew();
            while (!condition() && elapsed.Elapsed < TimeSpan.FromSeconds(5))
                await Task.Delay(1);
            Assert.That(condition(), Is.True, "Timed out waiting for execution completion.");
        }
    }
}
