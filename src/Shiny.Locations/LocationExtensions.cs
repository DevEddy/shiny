using Microsoft.Extensions.DependencyInjection;
using Shiny.Locations;

namespace Shiny;

public static class LocationExtensions
{
    public static IServiceCollection AddGeofencing<TDelegate>(this IServiceCollection services) where TDelegate : IGeofenceDelegate
    {
    #if APPLE || ANDROID
        return services.AddGeofencing(typeof(TDelegate));
    #endif
        return services;
    }
}