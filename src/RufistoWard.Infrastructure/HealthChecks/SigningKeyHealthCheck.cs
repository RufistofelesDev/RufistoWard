namespace RufistoWard.Tokens.HealthChecks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RufistoWard.Tokens.Persistence;

public sealed class SigningKeyHealthCheck(RufistoWardDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var activeKey = await dbContext.SigningKeys
            .Where(k => k.IsActive)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeKey is null)
        {
            return HealthCheckResult.Unhealthy("No active signing key found.");
        }

        if (activeKey.ExpiresAt.HasValue &&
            activeKey.ExpiresAt.Value < DateTime.UtcNow.AddDays(30))
        {
            return HealthCheckResult.Degraded(
                $"Signing key '{activeKey.KeyId}' expires at {activeKey.ExpiresAt.Value:O}.");
        }

        return HealthCheckResult.Healthy("Signing key is active and valid.");
    }
}
