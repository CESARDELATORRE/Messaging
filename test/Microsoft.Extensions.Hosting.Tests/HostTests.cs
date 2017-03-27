// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting.Fakes;
using Microsoft.Extensions.Options;
using Xunit;

namespace Microsoft.Extensions.Hosting
{
    public class HostTests
    {
        [Fact]
        public void HostWithNoServicesThrows()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => CreateBuilder().Build().Start());

            Assert.Equal("No service for type 'Microsoft.Extensions.Hosting.IHostedService' has been registered.", ex.Message);
        }

        [Fact]
        public void HostShutsDownWhenTokenTriggers()
        {
            var host = CreateBuilder().UseHostedService<FakeHostedService>().Build();
            var lifetime = host.Services.GetRequiredService<IApplicationLifetime>();
            var cts = new CancellationTokenSource();

            Task.Run(() => host.Run(cts.Token));

            Assert.True(lifetime.ApplicationStarted.WaitHandle.WaitOne(30000));

            cts.Cancel();

            Assert.True(lifetime.ApplicationStopped.WaitHandle.WaitOne(30000));
        }

        [Fact]
        public void HostApplicationLifetimeEventsOrderedCorrectlyDuringShutdown()
        {
            var host = CreateBuilder().UseHostedService<FakeHostedService>().Build();
            var lifetime = host.Services.GetRequiredService<IApplicationLifetime>();
            var applicationStartedEvent = new ManualResetEventSlim(false);
            var applicationStoppingEvent = new ManualResetEventSlim(false);
            var applicationStoppedEvent = new ManualResetEventSlim(false);
            var applicationStartedCompletedBeforeApplicationStopping = false;
            var applicationStoppingCompletedBeforeApplicationStopped = false;
            var applicationStoppedCompletedBeforeRunCompleted = false;

            lifetime.ApplicationStarted.Register(() =>
            {
                applicationStartedEvent.Set();
            });

            lifetime.ApplicationStopping.Register(() =>
            {
                // Check whether the applicationStartedEvent has been set
                applicationStartedCompletedBeforeApplicationStopping = applicationStartedEvent.IsSet;

                // Simulate work.
                Thread.Sleep(1000);

                applicationStoppingEvent.Set();
            });

            lifetime.ApplicationStopped.Register(() =>
            {
                // Check whether the applicationStoppingEvent has been set
                applicationStoppingCompletedBeforeApplicationStopped = applicationStoppingEvent.IsSet;
                applicationStoppedEvent.Set();
            });

            var runHostAndVerifyApplicationStopped = Task.Run(() =>
            {
                host.Run();
                // Check whether the applicationStoppingEvent has been set
                applicationStoppedCompletedBeforeRunCompleted = applicationStoppedEvent.IsSet;
            });

            // Wait until application has started to shut down the host
            Assert.True(applicationStartedEvent.Wait(5000));

            // Trigger host shutdown on a separate thread
            Task.Run(() => lifetime.StopApplication());

            // Wait for all events and host.Run() to complete
            Assert.True(runHostAndVerifyApplicationStopped.Wait(5000));

            // Verify Ordering
            Assert.True(applicationStartedCompletedBeforeApplicationStopping);
            Assert.True(applicationStoppingCompletedBeforeApplicationStopped);
            Assert.True(applicationStoppedCompletedBeforeRunCompleted);
        }

        [Fact]
        public void HostDisposesServiceProvider()
        {
            var host = CreateBuilder()
                .UseHostedService<FakeHostedService>()
                .ConfigureServices(s =>
                {
                    s.AddTransient<IFakeService, FakeService>();
                    s.AddSingleton<IFakeSingletonService, FakeService>();
                })
                .Build();

            host.Start();

            var singleton = (FakeService)host.Services.GetService<IFakeSingletonService>();
            var transient = (FakeService)host.Services.GetService<IFakeService>();

            Assert.False(singleton.Disposed);
            Assert.False(transient.Disposed);

            host.Dispose();

            Assert.True(singleton.Disposed);
            Assert.True(transient.Disposed);
        }

        [Fact]
        public void HostNotifiesApplicationStarted()
        {
            var host = CreateBuilder().UseHostedService<FakeHostedService>().Build();
            var applicationLifetime = host.Services.GetService<IApplicationLifetime>();

            Assert.False(applicationLifetime.ApplicationStarted.IsCancellationRequested);

            using (host)
            {
                host.Start();

                Assert.True(applicationLifetime.ApplicationStarted.IsCancellationRequested);
            }
        }

        [Fact]
        public void HostNotifiesAllIApplicationLifetimeCallbacksEvenIfTheyThrow()
        {
            var host = CreateBuilder().UseHostedService<FakeHostedService>().Build();
            var applicationLifetime = host.Services.GetService<IApplicationLifetime>();

            var started = RegisterCallbacksThatThrow(applicationLifetime.ApplicationStarted);
            var stopping = RegisterCallbacksThatThrow(applicationLifetime.ApplicationStopping);
            var stopped = RegisterCallbacksThatThrow(applicationLifetime.ApplicationStopped);

            using (host)
            {
                host.Start();
                Assert.True(applicationLifetime.ApplicationStarted.IsCancellationRequested);
                Assert.True(started.All(s => s));
                host.Dispose();
                Assert.True(stopping.All(s => s));
                Assert.True(stopped.All(s => s));
            }
        }

        [Fact]
        public void HostNotifiesAllIApplicationLifetimeEventsCallbacksEvenIfTheyThrow()
        {
            bool[] events1 = null;
            bool[] events2 = null;

            var host = CreateBuilder()
                .ConfigureServices(services =>
                {
                    events1 = RegisterCallbacksThatThrow(services);
                    events2 = RegisterCallbacksThatThrow(services);
                })
                .Build();

            using (host)
            {
                host.Start();
                Assert.True(events1[0]);
                Assert.True(events2[0]);
                host.Dispose();
                Assert.True(events1[1]);
                Assert.True(events2[1]);
            }
        }

        [Fact]
        public void HostStopApplicationDoesNotFireStopOnHostedService()
        {
            var stoppingCalls = 0;
            var host = CreateBuilder()
                .ConfigureServices(services =>
                {
                    Action started = () => { };
                    Action stopping = () => stoppingCalls++;

                    services.AddSingleton<IHostedService>(new DelegateHostedService(started, stopping));
                })
                .Build();
            var lifetime = host.Services.GetRequiredService<IApplicationLifetime>();
            lifetime.StopApplication();

            using (host)
            {
                host.Start();

                Assert.Equal(0, stoppingCalls);
            }
        }

        [Fact]
        public void HostedServiceCanInjectApplicationLifetime()
        {
            var host = CreateBuilder()
                   .ConfigureServices(services => services.AddSingleton<IHostedService, TestHostedService>())
                   .Build();
            var lifetime = host.Services.GetRequiredService<IApplicationLifetime>();
            lifetime.StopApplication();

            host.Start();
            var svc = (TestHostedService)host.Services.GetRequiredService<IHostedService>();
            Assert.True(svc.StartCalled);
            host.Dispose();
            Assert.True(svc.StopCalled);
        }

        [Fact]
        public void HostedServiceStartStopCalledDuringHostStartDispose()
        {
            var stoppingCalls = 0;
            var startedCalls = 0;
            var host = CreateBuilder()
                .ConfigureServices(services =>
                {
                    Action started = () => startedCalls++;
                    Action stopping = () => stoppingCalls++;

                    services.AddSingleton<IHostedService>(new DelegateHostedService(started, stopping));
                })
                .Build();
            var lifetime = host.Services.GetRequiredService<IApplicationLifetime>();

            using (host)
            {
                Assert.Equal(0, startedCalls);
                Assert.Equal(0, stoppingCalls);

                host.Start();
                Assert.Equal(1, startedCalls);
                Assert.Equal(0, stoppingCalls);

                host.Dispose();
                Assert.Equal(1, startedCalls);
                Assert.Equal(1, stoppingCalls);
            }
        }

        [Fact]
        public void HostNotifiesAllIHostedServicesAndIApplicationLifetimeCallbacksEvenIfTheyThrow()
        {
            bool[] events1 = null;
            bool[] events2 = null;
            var host = CreateBuilder()
                .ConfigureServices(services =>
                {
                    events1 = RegisterCallbacksThatThrow(services);
                    events2 = RegisterCallbacksThatThrow(services);
                })
                .Build();
            var applicationLifetime = host.Services.GetService<IApplicationLifetime>();
            var started = RegisterCallbacksThatThrow(applicationLifetime.ApplicationStarted);
            var stopping = RegisterCallbacksThatThrow(applicationLifetime.ApplicationStopping);

            using (host)
            {
                host.Start();
                Assert.True(events1[0]);
                Assert.True(events2[0]);
                Assert.True(started.All(s => s));

                host.Dispose();
                Assert.True(events1[1]);
                Assert.True(events2[1]);
                Assert.True(stopping.All(s => s));
            }
        }

        [Fact]
        public void HostInjectsHostingEnvironment()
        {
            var host = CreateBuilder()
                .UseHostedService<FakeHostedService>()
                .UseEnvironment("WithHostingEnvironment")
                .Build();

            using (host)
            {
                host.Start();

                var env = host.Services.GetService<IHostingEnvironment>();
                Assert.Equal("WithHostingEnvironment", env.EnvironmentName);
            }
        }

        [Fact]
        public void CanCreateApplicationServicesWithAddedServices()
        {
            var host = CreateBuilder().UseHostedService<FakeHostedService>().ConfigureServices(services => services.AddOptions()).Build();

            Assert.NotNull(host.Services.GetRequiredService<IOptions<object>>());
        }

        [Fact]
        public void EnvDefaultsToProductionIfNoConfig()
        {
            var host = CreateBuilder().UseHostedService<FakeHostedService>().Build();

            var env = host.Services.GetService<IHostingEnvironment>();

            Assert.Equal(EnvironmentName.Production, env.EnvironmentName);
        }

        [Fact]
        public void EnvDefaultsToConfigValueIfSpecified()
        {
            var vals = new Dictionary<string, string> { { HostDefaults.EnvironmentKey, "Staging" } };
            var builder = new ConfigurationBuilder().AddInMemoryCollection(vals);
            var config = builder.Build();
            var host = CreateBuilder(config).UseHostedService<FakeHostedService>().Build();

            var env = host.Services.GetService<IHostingEnvironment>();

            Assert.Equal("Staging", env.EnvironmentName);
        }

        [Fact]
        public void IsEnvironment_Extension_Is_Case_Insensitive()
        {
            var host = CreateBuilder().UseHostedService<FakeHostedService>().Build();
            using (host)
            {
                host.Start();
                var env = host.Services.GetRequiredService<IHostingEnvironment>();
                Assert.True(env.IsEnvironment(EnvironmentName.Production));
                Assert.True(env.IsEnvironment("producTion"));
            }
        }

        private IHostBuilder CreateBuilder(IConfiguration config = null)
            => new HostBuilder().UseSetting(HostDefaults.ApplicationKey, "UnitTests")
                                .UseConfiguration(config ?? new ConfigurationBuilder().Build());

         private static bool[] RegisterCallbacksThatThrow(IServiceCollection services)
        {
            bool[] events = new bool[2];

            Action started = () =>
            {
                events[0] = true;
                throw new InvalidOperationException();
            };

            Action stopping = () =>
            {
                events[1] = true;
                throw new InvalidOperationException();
            };

            services.AddSingleton<IHostedService>(new DelegateHostedService(started, stopping));

            return events;
        }

        private static bool[] RegisterCallbacksThatThrow(CancellationToken token)
        {
            var signals = new bool[3];
            for (int i = 0; i < signals.Length; i++)
            {
                token.Register(state =>
                {
                    signals[(int)state] = true;
                    throw new InvalidOperationException();
                }, i);
            }

            return signals;
        }

        private class TestHostedService : IHostedService
        {
            private readonly IApplicationLifetime _lifetime;

            public TestHostedService(IApplicationLifetime lifetime)
            {
                _lifetime = lifetime;
            }

            public bool StartCalled { get; set; }
            public bool StopCalled { get; set; }

            public void Start()
            {
                StartCalled = true;
            }

            public void Stop()
            {
                StopCalled = true;
            }
        }

        private class DelegateHostedService : IHostedService
        {
            private readonly Action _started;
            private readonly Action _stopping;

            public DelegateHostedService(Action started, Action stopping)
            {
                _started = started;
                _stopping = stopping;
            }

            public void Start() => _started();

            public void Stop() => _stopping();
        }
    }
}