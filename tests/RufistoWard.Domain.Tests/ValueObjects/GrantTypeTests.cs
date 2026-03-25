namespace RufistoWard.Identity.Tests.ValueObjects;

using RufistoWard.Identity.ValueObjects;

[Trait("Category", "Unit")]
public sealed class GrantTypeTests
{
    [Theory]
    [InlineData("authorization_code")]
    [InlineData("client_credentials")]
    [InlineData("refresh_token")]
    [InlineData("urn:ietf:params:oauth:grant-type:device_code")]
    public void ValidGrantType_CreatesSuccessfully(string value)
    {
        var grantType = new GrantType(value);
        Assert.Equal(value, grantType.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NullOrEmpty_ThrowsArgumentException(string? value)
    {
        Assert.ThrowsAny<ArgumentException>(() => new GrantType(value!));
    }

    [Fact]
    public void ImplicitStringConversion_ReturnsValue()
    {
        var grantType = new GrantType("authorization_code");
        string result = grantType;
        Assert.Equal("authorization_code", result);
    }
}
