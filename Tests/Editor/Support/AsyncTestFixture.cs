using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace EcaSystems.Tests.Support
{
    public abstract class AsyncTestFixture
    {
        private readonly List<IDisposable> _resources = new();

        protected T Track<T>(T resource) where T : IDisposable
        {
            _resources.Add(resource);
            return resource;
        }

        [TearDown]
        public void ReleaseControlledActions()
        {
            foreach (var resource in _resources) resource.Dispose();
            _resources.Clear();
        }
    }
}
