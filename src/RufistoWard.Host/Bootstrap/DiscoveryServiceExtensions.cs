namespace RufistoWard.Core.Bootstrap;

using RufistoWard.Auth.Protocol.Discovery;
using RufistoWard.Core.Options;

public static class DiscoveryServiceExtensions
{
    public static IServiceCollection AddRufistoWardDiscovery(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<DiscoveryOptions>(opts =>
        {
            opts.Issuer = configuration.GetSection("Token")["Issuer"]
                ?? throw new InvalidOperationException("Token:Issuer configuration is required.");
        });

        services.AddScoped<IDiscoveryService, DiscoveryService>();
        services.AddScoped<IJwksService, JwksService>();

        return services;
    }
}
