﻿using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;

namespace Pixeval.Startup.WinUI.Hosting
{
    internal class WinUIAppStartupService : IHostedService
    {

        private readonly IServiceProvider _serviceProvider;
        private readonly IHostApplicationLifetime _applicationLifetime;
        private Application _app;
        public WinUIAppStartupService(IServiceProvider serviceProvider, IHostApplicationLifetime applicationLifetime)
        {
            _serviceProvider = serviceProvider;
            _applicationLifetime = applicationLifetime;

        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Application.Start(p =>
            {
                try
                {
                    var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
                    SynchronizationContext.SetSynchronizationContext(context);
                    _app = _serviceProvider.GetRequiredService<Application>();
                }
                catch (Exception e)
                {
                    if (Debugger.IsAttached)
                    {
                        Debugger.Break();
                    }
                }
                
            });
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _app.Exit();
            return Task.CompletedTask;
        }
    }
}
