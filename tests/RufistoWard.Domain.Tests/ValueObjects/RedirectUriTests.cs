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

    [Theory]
    [InlineData("javascript:alert(document.cookie)")]
    [InlineData("JavaScript:alert(1)")]
    [InlineData(" javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("file:///etc/passwd")]
    public void ScriptAndLocalSchemes_AreRefused(string value)
    {
        // These are echoed into Location headers and form_post actions; registering one turns the
        // IdP into a script launcher on its own origin.
        Assert.NotNull(RedirectUri.Validate(value));
        Assert.Throws<ArgumentException>(() => new RedirectUri(value));
    }

    [Theory]
    [InlineData("https://example.com/callback")]
    [InlineData("com.example.app:/oauth2redirect")]
    [InlineData("myapp://callback")]
    public void HttpAndNativeAppSchemes_StayAllowed(string value)
    {
        Assert.Null(RedirectUri.Validate(value));
    }
}
