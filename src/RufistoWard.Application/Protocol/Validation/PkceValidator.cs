namespace RufistoWard.Auth.Protocol.Validation;

using System.Security.Cryptography;
using System.Text;

public static class PkceValidator
{
    public static bool ValidateS256(string codeVerifier, string codeChallenge)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        var computed = Base64UrlEncode(hash);
        return string.Equals(computed, codeChallenge, StringComparison.Ordinal);
    }

    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
