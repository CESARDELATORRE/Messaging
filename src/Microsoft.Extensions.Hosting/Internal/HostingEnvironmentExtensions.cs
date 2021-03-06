﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.Extensions.Hosting.Internal
{
    public static class HostingEnvironmentExtensions
    {
        public static void Initialize(this IHostingEnvironment hostingEnvironment, string applicationName, HostOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            if (string.IsNullOrEmpty(applicationName))
            {
                throw new ArgumentException("A valid non-empty application name must be provided.", nameof(applicationName));
            }

            hostingEnvironment.ApplicationName = applicationName;
            hostingEnvironment.EnvironmentName = options.Environment ?? hostingEnvironment.EnvironmentName;
        }
    }
}
