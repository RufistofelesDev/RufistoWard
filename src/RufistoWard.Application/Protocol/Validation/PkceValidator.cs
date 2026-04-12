namespace RufistoWard.Auth.Protocol.Validation;

using System.Security.Cryptography;
using System.Text;
using RufistoWard.Auth.Protocol.Common;

public static class PkceValidator
{
    public static bool ValidateS256(string codeVerifier, string codeChallenge)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        var computed = Base64Url.Encode(hash);
        return string.Equals(computed, codeChallenge, StringComparison.Ordinal);
    }
}
