// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Extensions.Hosting.Internal
{
    public class HostingEnvironment : IHostingEnvironment
    {
        public string ApplicationName { get; set; }

        public string EnvironmentName { get; set; } = Hosting.EnvironmentName.Production;
    }
}