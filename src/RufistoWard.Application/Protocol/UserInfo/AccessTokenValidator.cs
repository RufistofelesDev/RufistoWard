namespace RufistoWard.Auth.Protocol.UserInfo;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RufistoWard.Auth.Protocol.Common;
using RufistoWard.Auth.Protocol.Discovery;
using RufistoWard.Identity.Interfaces;

public sealed record ValidatedToken(string Subject, string[] Scopes, string ClientId, string Issuer);

public interface IAccessTokenValidator
{
    Task<Result<ValidatedToken>> ValidateAsync(string token, CancellationToken cancellationToken = default);
}

public sealed class AccessTokenValidator(
    ISigningKeyRepository signingKeyRepository,
    IOptions<DiscoveryOptions> discoveryOptions) : IAccessTokenValidator
{
    public async Task<Result<ValidatedToken>> ValidateAsync(string token, CancellationToken cancellationToken = default)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
            return Result<ValidatedToken>.Failure(ProtocolError.InvalidToken("The access token is malformed."));

        // Decode header to get kid and algorithm
        JsonElement header;
        JsonElement payload;
        byte[] signatureBytes;
        try
        {
            header = DecodeSegment(parts[0]);
            payload = DecodeSegment(parts[1]);
            signatureBytes = Base64UrlDecode(parts[2]);
        }
        catch
        {
            return Result<ValidatedToken>.Failure(ProtocolError.InvalidToken("The access token is malformed."));
        }

        // Verify signature
        var kid = header.TryGetProperty("kid", out var kidProp) ? kidProp.GetString() : null;
        var alg = header.TryGetProperty("alg", out var algProp) ? algProp.GetString() : null;

        if (string.IsNullOrEmpty(kid) || string.IsNullOrEmpty(alg))
            return Result<ValidatedToken>.Failure(ProtocolError.InvalidToken("The access token is missing required header claims."));

        var signingKey = await signingKeyRepository.FindByKeyIdAsync(kid, cancellationToken);
        signingKey ??= await signingKeyRepository.GetActiveKeyAsync(cancellationToken);

        if (signingKey is null)
            return Result<ValidatedToken>.Failure(ProtocolError.InvalidToken("The signing key could not be resolved."));

        var signingInput = Encoding.UTF8.GetBytes($"{parts[0]}.{parts[1]}");
        if (!VerifySignature(signingInput, signatureBytes, signingKey.KeyType, alg, signingKey.SerializedKey))
            return Result<ValidatedToken>.Failure(ProtocolError.InvalidToken("The access token signature is invalid."));

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Validate expiration
        var exp = payload.TryGetProperty("exp", out var expProp) ? expProp.GetInt64() : 0;
        if (exp <= now)
            return Result<ValidatedToken>.Failure(ProtocolError.InvalidToken("The access token has expired."));

        // Validate not-before (nbf) — reject tokens that are not yet valid
        if (payload.TryGetProperty("nbf", out var nbfProp) && nbfProp.GetInt64() > now)
            return Result<ValidatedToken>.Failure(ProtocolError.InvalidToken("The access token is not yet valid."));

        // Validate issuer
        var tokenIssuer = payload.TryGetProperty("iss", out var issProp) ? issProp.GetString() : null;
        var expectedIssuer = discoveryOptions.Value.Issuer;
        if (tokenIssuer != expectedIssuer)
            return Result<ValidatedToken>.Failure(ProtocolError.InvalidToken("The access token issuer is invalid."));

        // Extract claims
        var sub = payload.TryGetProperty("sub", out var subProp) ? subProp.GetString() : null;
        if (string.IsNullOrEmpty(sub))
            return Result<ValidatedToken>.Failure(ProtocolError.InvalidToken("The access token is missing the sub claim."));

        var scopeStr = payload.TryGetProperty("scope", out var scopeProp) ? scopeProp.GetString() : "";
        var scopes = (scopeStr ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var clientId = payload.TryGetProperty("aud", out var audProp) ? audProp.GetString() ?? "" : "";

        return Result<ValidatedToken>.Success(new ValidatedToken(sub, scopes, clientId, tokenIssuer ?? ""));
    }

    private static bool VerifySignature(byte[] data, byte[] signature, string keyType, string algorithm, string serializedKey)
    {
        var keyBytes = Convert.FromBase64String(serializedKey);

        if (string.Equals(keyType, "RSA", StringComparison.OrdinalIgnoreCase))
        {
            using var rsa = RSA.Create();
            rsa.ImportRSAPrivateKey(keyBytes, out _);
            var hashAlgorithm = algorithm switch
            {
                "RS256" => HashAlgorithmName.SHA256,
                "RS384" => HashAlgorithmName.SHA384,
                "RS512" => HashAlgorithmName.SHA512,
                _ => HashAlgorithmName.SHA256,
            };
            return rsa.VerifyData(data, signature, hashAlgorithm, RSASignaturePadding.Pkcs1);
        }

        if (string.Equals(keyType, "EC", StringComparison.OrdinalIgnoreCase))
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportECPrivateKey(keyBytes, out _);
            var hashAlgorithm = algorithm switch
            {
                "ES256" => HashAlgorithmName.SHA256,
                "ES384" => HashAlgorithmName.SHA384,
                "ES512" => HashAlgorithmName.SHA512,
                _ => HashAlgorithmName.SHA256,
            };
            return ecdsa.VerifyData(data, signature, hashAlgorithm);
        }

        return false;
    }

    private static JsonElement DecodeSegment(string segment)
    {
        var padded = segment.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
        return JsonDocument.Parse(json).RootElement;
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        return Convert.FromBase64String(padded);
    }
}
