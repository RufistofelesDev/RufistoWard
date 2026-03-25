namespace RufistoWard.Identity.Tests.ValueObjects;

using RufistoWard.Identity.ValueObjects;

[Trait("Category", "Unit")]
public sealed class TokenLifetimeTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3600)]
    [InlineData(2592000)]
    public void ValidSeconds_CreatesSuccessfully(int seconds)
    {
        var lifetime = new TokenLifetime(seconds);
        Assert.Equal(seconds, lifetime.Seconds);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void ZeroOrNegative_ThrowsArgumentException(int seconds)
    {
        Assert.Throws<ArgumentException>(() => new TokenLifetime(seconds));
    }

    [Fact]
    public void ExceedsMax_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new TokenLifetime(2592001));
    }

    [Fact]
    public void ImplicitIntConversion_ReturnsSeconds()
    {
        var lifetime = new TokenLifetime(3600);
        int result = lifetime;
        Assert.Equal(3600, result);
    }
}
