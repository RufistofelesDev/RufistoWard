namespace RufistoWard.Auth.Protocol.Token;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RufistoWard.Auth.Protocol.Common;
using RufistoWard.Identity.Interfaces;

public sealed class JwtTokenBuilder(ISigningKeyRepository signingKeyRepository) : ITokenBuilder
{
    public async Task<string> BuildAccessTokenAsync(TokenBuildContext context, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var payload = new Dictionary<string, object>
        {
            ["iss"] = context.Issuer,
            ["sub"] = context.Subject,
            ["aud"] = context.ClientId,
            ["exp"] = now.AddSeconds(context.LifetimeSeconds).ToUnixTimeSeconds(),
            ["nbf"] = now.ToUnixTimeSeconds(),
            ["iat"] = now.ToUnixTimeSeconds(),
            ["jti"] = Guid.NewGuid().ToString("N"),
            ["scope"] = string.Join(' ', context.Scopes),
        };

        foreach (var claim in context.AdditionalClaims)
        {
            payload[claim.Key] = claim.Value;
        }

        return await SignJwtAsync(payload, cancellationToken);
    }

    public async Task<string> BuildIdTokenAsync(TokenBuildContext context, string accessToken, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var payload = new Dictionary<string, object>
        {
            ["iss"] = context.Issuer,
            ["sub"] = context.Subject,
            ["aud"] = context.ClientId,
            ["exp"] = now.AddSeconds(context.LifetimeSeconds).ToUnixTimeSeconds(),
            ["nbf"] = now.ToUnixTimeSeconds(),
            ["iat"] = now.ToUnixTimeSeconds(),
            ["auth_time"] = (context.AuthTime ?? now).ToUnixTimeSeconds(),
            ["at_hash"] = ComputeAtHash(accessToken),
        };

        if (context.Nonce is not null)
        {
            payload["nonce"] = context.Nonce;
        }

        foreach (var claim in context.AdditionalClaims)
        {
            payload[claim.Key] = claim.Value;
        }

        return await SignJwtAsync(payload, cancellationToken);
    }

    private async Task<string> SignJwtAsync(Dictionary<string, object> payload, CancellationToken cancellationToken)
    {
        var signingKey = await signingKeyRepository.GetActiveKeyAsync(cancellationToken)
            ?? throw new InvalidOperationException("No active signing key available.");

        var header = new Dictionary<string, string>
        {
            ["alg"] = signingKey.Algorithm,
            ["typ"] = "JWT",
            ["kid"] = signingKey.KeyId,
        };

        var headerJson = JsonSerializer.Serialize(header);
        var payloadJson = JsonSerializer.Serialize(payload);

        var headerB64 = Base64Url.Encode(Encoding.UTF8.GetBytes(headerJson));
        var payloadB64 = Base64Url.Encode(Encoding.UTF8.GetBytes(payloadJson));

        var signingInput = $"{headerB64}.{payloadB64}";
        var signature = Sign(signingInput, signingKey.KeyType, signingKey.Algorithm, signingKey.SerializedKey);

        return $"{signingInput}.{signature}";
    }

    private static string Sign(string input, string keyType, string algorithm, string serializedKey)
    {
        var data = Encoding.UTF8.GetBytes(input);
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
            var sig = rsa.SignData(data, hashAlgorithm, RSASignaturePadding.Pkcs1);
            return Base64Url.Encode(sig);
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
            var sig = ecdsa.SignData(data, hashAlgorithm);
            return Base64Url.Encode(sig);
        }

        throw new NotSupportedException($"Unsupported key type: {keyType}");
    }

    private static string ComputeAtHash(string accessToken)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(accessToken));
        var leftHalf = hash[..(hash.Length / 2)];
        return Base64Url.Encode(leftHalf);
    }

}
