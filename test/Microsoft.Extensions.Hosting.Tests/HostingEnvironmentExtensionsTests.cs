// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Hosting.Internal;
using Xunit;

namespace Microsoft.Extensions.Hosting.Tests
{
    public class HostingEnvironmentExtensionsTests
    {
        [Fact]
        public void OverridesEnvironmentFromConfig()
        {
            var env = new HostingEnvironment() { EnvironmentName = "SomeName" };

            env.Initialize("DummyApplication", new HostOptions() { Environment = "NewName" });

            Assert.Equal("NewName", env.EnvironmentName);
        }
    }
}
