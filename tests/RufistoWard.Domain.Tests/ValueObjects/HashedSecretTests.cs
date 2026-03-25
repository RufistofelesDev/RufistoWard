namespace RufistoWard.Identity.Tests.ValueObjects;

using RufistoWard.Identity.ValueObjects;

[Trait("Category", "Unit")]
public sealed class HashedSecretTests
{
    [Fact]
    public void ValidHash_CreatesSuccessfully()
    {
        var secret = new HashedSecret("sha256=abc123def456");
        Assert.Equal("sha256=abc123def456", secret.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NullOrEmpty_ThrowsArgumentException(string? value)
    {
        Assert.ThrowsAny<ArgumentException>(() => new HashedSecret(value!));
    }

    [Fact]
    public void ToString_ReturnsMasked()
    {
        var secret = new HashedSecret("sensitive-hash");
        Assert.Equal("***", secret.ToString());
    }
}
