namespace RufistoWard.Identity.Tests.ValueObjects;

using RufistoWard.Identity.ValueObjects;

[Trait("Category", "Unit")]
public sealed class ScopeNameTests
{
    [Theory]
    [InlineData("openid")]
    [InlineData("api.read")]
    [InlineData("profile")]
    public void ValidScopeName_CreatesSuccessfully(string value)
    {
        var scope = new ScopeName(value);
        Assert.Equal(value, scope.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NullOrEmpty_ThrowsArgumentException(string? value)
    {
        Assert.ThrowsAny<ArgumentException>(() => new ScopeName(value!));
    }

    [Fact]
    public void ExceedsMaxLength_ThrowsArgumentException()
    {
        var longValue = new string('a', 201);
        Assert.Throws<ArgumentException>(() => new ScopeName(longValue));
    }

    [Fact]
    public void ContainsSpaces_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new ScopeName("scope name"));
    }
}
