// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.Hosting.Internal
{
    public class Host : IHost
    {
        private readonly IServiceCollection _applicationServiceCollection;
        private ApplicationLifetime _applicationLifetime;
        private HostedServiceExecutor _hostedServiceExecutor;

        private readonly IServiceProvider _hostingServiceProvider;
        private readonly HostOptions _options;
        private readonly IConfiguration _config;

        private IServiceProvider _applicationServices;
        private ILogger<Host> _logger;

        public Host(
            IServiceCollection appServices,
            IServiceProvider hostingServiceProvider,
            HostOptions options,
            IConfiguration config)
        {
            _applicationServiceCollection = appServices ?? throw new ArgumentNullException(nameof(appServices));
            _hostingServiceProvider = hostingServiceProvider ?? throw new ArgumentNullException(nameof(hostingServiceProvider));
            _options = options;
            _config = config ?? throw new ArgumentNullException(nameof(config));

            _applicationServiceCollection.AddSingleton<IApplicationLifetime, ApplicationLifetime>();
            _applicationServiceCollection.AddSingleton<HostedServiceExecutor>();
        }

        public IServiceProvider Services
        {
            get
            {
                EnsureApplicationServices();
                return _applicationServices;
            }
        }

        public void Initialize()
        {
            EnsureApplicationServices();
        }

        public virtual void Start()
        {
            HostingEventSource.Log.HostStart();
            _logger = _applicationServices.GetRequiredService<ILogger<Host>>();
            _logger.Starting();

            Initialize();

            _applicationLifetime = _applicationServices.GetRequiredService<IApplicationLifetime>() as ApplicationLifetime;
            _hostedServiceExecutor = _applicationServices.GetRequiredService<HostedServiceExecutor>();
            var diagnosticSource = _applicationServices.GetRequiredService<DiagnosticSource>();
            //var httpContextFactory = _applicationServices.GetRequiredService<IHttpContextFactory>();
            //Server.Start(new HostingApplication(_application, _logger, diagnosticSource, httpContextFactory));

            // Fire IApplicationLifetime.Started
            _applicationLifetime?.NotifyStarted();

            // Fire IHostedService.Start
            _hostedServiceExecutor.Start();

            _logger.Started();
        }

        private void EnsureApplicationServices()
        {
            if (_applicationServices == null)
            {
                _applicationServices = _applicationServiceCollection.BuildServiceProvider();
            }
        }

        public void Dispose()
        {
            _logger?.Shutdown();

            // Fire IApplicationLifetime.Stopping
            _applicationLifetime?.StopApplication();

            // Fire the IHostedService.Stop
            _hostedServiceExecutor?.Stop();

            (_hostingServiceProvider as IDisposable)?.Dispose();
            (_applicationServices as IDisposable)?.Dispose();

            // Fire IApplicationLifetime.Stopped
            _applicationLifetime?.NotifyStopped();

            HostingEventSource.Log.HostStop();
        }
    }
}
