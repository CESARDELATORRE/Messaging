// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace SimpleConsoleHostSample
{
    class Program
    {
        static void Main(string[] args)
        {
            var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSingleton<IHostedService, MyHostedService>();
                })
                .Build();

            host.Run();
        }
    }

    class MyHostedService : IHostedService
    {
        public void Start()
        {
            Console.WriteLine("Hosted service is starting...");
        }

        public void Stop()
        {
            Console.WriteLine("Hosted service is stopping...");
        }
    }
}
