namespace RufistoWard.Auth.Tests.Protocol.Validation;

using System.Security.Cryptography;
using System.Text;
using RufistoWard.Auth.Protocol.Validation;

[Trait("Category", "Unit")]
public sealed class PkceValidatorTests
{
    [Fact]
    public void ValidS256_ReturnsTrue()
    {
        var verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
        var challenge = ComputeS256Challenge(verifier);

        Assert.True(PkceValidator.ValidateS256(verifier, challenge));
    }

    [Fact]
    public void InvalidVerifier_ReturnsFalse()
    {
        var verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
        var challenge = ComputeS256Challenge(verifier);

        Assert.False(PkceValidator.ValidateS256("wrong-verifier", challenge));
    }

    [Fact]
    public void EmptyVerifier_ReturnsFalse()
    {
        Assert.False(PkceValidator.ValidateS256("", "some-challenge"));
    }

    private static string ComputeS256Challenge(string verifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
