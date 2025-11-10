using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Shiny.Hosting;


public class WindowsLifecycleExecutor
{
    readonly ILogger logger;
    readonly WindowsPlatform platform;
    readonly IEnumerable<IWindowsLifecycle.IApplicationLifecycle> appHandlers;
    
    public WindowsLifecycleExecutor(
        ILogger<WindowsLifecycleExecutor> logger,
        WindowsPlatform platform,
        IEnumerable<IWindowsLifecycle.IApplicationLifecycle> appHandlers
    )
    {
        this.logger = logger;
        this.platform = platform;
        this.appHandlers = appHandlers;
    }
    
    public void OnWindowCreated(Microsoft.UI.Xaml.Window window)
    {
        this.Execute(this.appHandlers, handler => handler.OnWindowCreated(window));
    }

    private void Execute<T>(IEnumerable<T> services, Action<T> action)
    {
        foreach (var handler in services)
        {
            try
            {
                action(handler);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to execute lifecycle call");
            }
        }
    }
}
