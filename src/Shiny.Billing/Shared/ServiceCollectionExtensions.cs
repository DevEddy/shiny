using Microsoft.Extensions.DependencyInjection;

namespace Shiny;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Native Billing services 
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddBilling(this IServiceCollection services)
    {
#if ANDROID || APPLE || WINDOWS
        services.AddShinyService<InAppBilling>();
#endif
        return services;
    }
    
    #if ANDROID
    public static IServiceCollection AddBillingAmazon(this IServiceCollection services)
    {
#if ANDROID || APPLE || WINDOWS
        services.AddShinyService<InAppBillingAmazon>();
#endif
        return services;
    }
    #endif
}
