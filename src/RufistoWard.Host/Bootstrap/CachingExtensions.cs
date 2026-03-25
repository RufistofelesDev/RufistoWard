namespace RufistoWard.Core.Bootstrap;

public static class CachingExtensions
{
    public static IServiceCollection AddRufistoWardCaching(this IServiceCollection services)
    {
        services.AddHybridCache();
        return services;
    }
}
