namespace RufistoWard.Core.Bootstrap;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using RufistoWard.Tokens.HealthChecks;
using RufistoWard.Tokens.Persistence;

public static class HealthCheckExtensions
{
    public static IServiceCollection AddRufistoWardHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddDbContextCheck<RufistoWardDbContext>(
                name: "database",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["db", "ready"])
            .AddCheck<SigningKeyHealthCheck>(
                name: "signing-key",
                failureStatus: HealthStatus.Degraded,
                tags: ["keys"]);

        return services;
    }
}
