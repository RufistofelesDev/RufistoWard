namespace RufistoWard.Auth.Protocol.Keys;

public interface IKeyRotationService
{
    Task RotateKeyAsync(CancellationToken cancellationToken = default);
    TimeSpan CalculateOverlapWindow(int accessTokenLifetimeSeconds, int refreshTokenLifetimeSeconds);
}
