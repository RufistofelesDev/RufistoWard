namespace RufistoWard.Identity.Tests.ValueObjects;

using RufistoWard.Identity.ValueObjects;

[Trait("Category", "Unit")]
public sealed class ClientIdTests
{
    [Theory]
    [InlineData("my-client")]
    [InlineData("client_123")]
    [InlineData("ABC")]
    public void ValidClientId_CreatesSuccessfully(string value)
    {
        var clientId = new ClientId(value);
        Assert.Equal(value, clientId.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NullOrEmpty_ThrowsArgumentException(string? value)
    {
        Assert.ThrowsAny<ArgumentException>(() => new ClientId(value!));
    }

    [Fact]
    public void ExceedsMaxLength_ThrowsArgumentException()
    {
        var longValue = new string('a', 201);
        Assert.Throws<ArgumentException>(() => new ClientId(longValue));
    }

    [Theory]
    [InlineData("client id")]
    [InlineData("client@id")]
    [InlineData("client.id")]
    public void InvalidCharacters_ThrowsArgumentException(string value)
    {
        Assert.Throws<ArgumentException>(() => new ClientId(value));
    }

    [Fact]
    public void ImplicitStringConversion_ReturnsValue()
    {
        var clientId = new ClientId("test-client");
        string result = clientId;
        Assert.Equal("test-client", result);
    }
}
