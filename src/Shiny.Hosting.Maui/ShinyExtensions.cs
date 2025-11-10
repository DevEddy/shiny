using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.LifecycleEvents;
using Shiny.Hosting;
using Shiny.Infrastructure;
using Shiny.Platforms.Apple;

namespace Shiny;


public static class ShinyExtensions
{
    public static MauiAppBuilder UseShiny(this MauiAppBuilder builder)
    {
        builder.Services.AddSingleton<IMauiInitializeService, ShinyMauiInitializationService>();

        builder.ConfigureLifecycleEvents(events =>
        {
#if ANDROID
            events.AddAndroid(android => android
                // Shiny will supply app foreground/background events
                // .OnResume()
                .OnApplicationCreate(_ =>
                {
                })
                .OnCreate(AndroidShinyHost.OnActivityOnCreate)
                .OnRequestPermissionsResult(AndroidShinyHost.OnRequestPermissionsResult)
                .OnActivityResult(AndroidShinyHost.OnActivityResult)
                .OnNewIntent(AndroidShinyHost.OnNewIntent)
            );
#elif APPLE
            // Shiny will supply push events & handle background url for http transfers
            events.AddiOS(ios => ios
                .WillEnterForeground(_ => {})
                .DidEnterBackground(_ => { })
                .ContinueUserActivity((_, activity, handler) => IosShinyHost.OnContinueUserActivity(activity, handler))
            );
#elif WINDOWS
            events.AddWindows(win => win
                .OnLaunching((app, args) => { })
                .OnClosed((app, args) => { })
                .OnVisibilityChanged((app, args) => { })
                .OnWindowCreated(window =>
                {
                    Host.Lifecycle.OnWindowCreated(window);
                })
            );
#endif
        });

        return builder;
    }
}
