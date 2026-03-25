namespace RufistoWard.Identity.Tests.ValueObjects;

using RufistoWard.Identity.ValueObjects;

[Trait("Category", "Unit")]
public sealed class RedirectUriTests
{
    [Theory]
    [InlineData("https://example.com/callback")]
    [InlineData("http://localhost:3000/auth")]
    [InlineData("myapp://callback")]
    public void ValidAbsoluteUri_CreatesSuccessfully(string value)
    {
        var uri = new RedirectUri(value);
        Assert.Equal(value, uri.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NullOrEmpty_ThrowsArgumentException(string? value)
    {
        Assert.ThrowsAny<ArgumentException>(() => new RedirectUri(value!));
    }

    [Theory]
    [InlineData("not-a-uri")]
    [InlineData("/relative/path")]
    public void InvalidUri_ThrowsArgumentException(string value)
    {
        Assert.Throws<ArgumentException>(() => new RedirectUri(value));
    }
}
