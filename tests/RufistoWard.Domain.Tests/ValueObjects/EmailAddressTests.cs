namespace RufistoWard.Identity.Tests.ValueObjects;

using RufistoWard.Identity.ValueObjects;

[Trait("Category", "Unit")]
public sealed class EmailAddressTests
{
    [Theory]
    [InlineData("user@example.com")]
    [InlineData("Admin@Domain.Org")]
    public void ValidEmail_CreatesSuccessfully(string value)
    {
        var email = new EmailAddress(value);
        Assert.Equal(value.ToLowerInvariant(), email.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NullOrEmpty_ThrowsArgumentException(string? value)
    {
        Assert.ThrowsAny<ArgumentException>(() => new EmailAddress(value!));
    }

    [Theory]
    [InlineData("notanemail")]
    [InlineData("missing@dot")]
    [InlineData("missing.at")]
    public void InvalidFormat_ThrowsArgumentException(string value)
    {
        Assert.Throws<ArgumentException>(() => new EmailAddress(value));
    }

    [Fact]
    public void ExceedsMaxLength_ThrowsArgumentException()
    {
        var longEmail = new string('a', 251) + "@b.com";
        Assert.Throws<ArgumentException>(() => new EmailAddress(longEmail));
    }
}
