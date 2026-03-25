namespace RufistoWard.Auth.Protocol.Keys;

using Microsoft.Extensions.Caching.Hybrid;
using RufistoWard.Identity.Interfaces;

public sealed class KeyRotationService(
    ISigningKeyRepository signingKeyRepository,
    IKeyGenerationService keyGenerationService,
    HybridCache cache) : IKeyRotationService
{
    public async Task RotateKeyAsync(CancellationToken cancellationToken = default)
    {
        var currentKey = await signingKeyRepository.GetActiveKeyAsync(cancellationToken);

        if (currentKey is not null)
        {
            var overlap = CalculateOverlapWindow(3600, 86400);
            currentKey.SetExpiration(DateTime.UtcNow.Add(overlap));
            await signingKeyRepository.UpdateAsync(currentKey, cancellationToken);
        }

        var algorithm = currentKey?.Algorithm ?? "RS256";
        await keyGenerationService.GenerateKeyAsync(algorithm, cancellationToken);

        await cache.RemoveAsync("oidc:jwks-document", cancellationToken);
    }

    public TimeSpan CalculateOverlapWindow(int accessTokenLifetimeSeconds, int refreshTokenLifetimeSeconds)
    {
        var maxLifetime = Math.Max(accessTokenLifetimeSeconds, refreshTokenLifetimeSeconds);
        return TimeSpan.FromSeconds(maxLifetime * 2);
    }
}
