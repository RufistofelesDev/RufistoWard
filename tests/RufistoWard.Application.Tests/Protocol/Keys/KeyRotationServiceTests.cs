namespace RufistoWard.Auth.Tests.Protocol.Keys;

using RufistoWard.Auth.Protocol.Keys;

[Trait("Category", "Unit")]
public sealed class KeyRotationServiceTests
{
    [Fact]
    public void CalculateOverlapWindow_ReturnsDoubleMaxLifetime()
    {
        // Use a dummy service just for the calculation method
        var service = CreateService();

        var overlap = service.CalculateOverlapWindow(
            accessTokenLifetimeSeconds: 3600,
            refreshTokenLifetimeSeconds: 86400);

        Assert.Equal(TimeSpan.FromSeconds(172800), overlap); // 2 * 86400
    }

    [Fact]
    public void CalculateOverlapWindow_UsesAccessTokenWhenLarger()
    {
        var service = CreateService();

        var overlap = service.CalculateOverlapWindow(
            accessTokenLifetimeSeconds: 100000,
            refreshTokenLifetimeSeconds: 3600);

        Assert.Equal(TimeSpan.FromSeconds(200000), overlap); // 2 * 100000
    }

    private static KeyRotationService CreateService()
    {
        // Minimal construction for testing calculation methods
        return new KeyRotationService(null!, null!, null!);
    }
}
