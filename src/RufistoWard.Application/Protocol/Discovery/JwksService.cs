namespace RufistoWard.Auth.Protocol.Discovery;

using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Hybrid;
using RufistoWard.Auth.Protocol.Common;
using RufistoWard.Identity.Interfaces;

public sealed class JwksService(
    ISigningKeyRepository signingKeyRepository,
    HybridCache cache) : IJwksService
{
    public async Task<JwksDocument> GetJwksDocumentAsync(CancellationToken cancellationToken = default)
    {
        return await cache.GetOrCreateAsync(
            "oidc:jwks-document",
            async ct => await BuildJwksDocumentAsync(ct),
            cancellationToken: cancellationToken);
    }

    private async Task<JwksDocument> BuildJwksDocumentAsync(CancellationToken cancellationToken)
    {
        var keys = await signingKeyRepository.GetAllAsync(cancellationToken);
        var activeKeys = keys.Where(k => k.IsActive).ToList();

        var jwks = new List<JsonWebKey>();

        foreach (var key in activeKeys)
        {
            var jwk = ExportPublicKey(key.KeyId, key.Algorithm, key.KeyType, key.SerializedKey);
            if (jwk is not null)
            {
                jwks.Add(jwk);
            }
        }

        return new JwksDocument { Keys = [.. jwks] };
    }

    private static JsonWebKey? ExportPublicKey(string keyId, string algorithm, string keyType, string serializedKey)
    {
        if (string.Equals(keyType, "RSA", StringComparison.OrdinalIgnoreCase))
        {
            return ExportRsaPublicKey(keyId, algorithm, serializedKey);
        }

        if (string.Equals(keyType, "EC", StringComparison.OrdinalIgnoreCase))
        {
            return ExportEcPublicKey(keyId, algorithm, serializedKey);
        }

        return null;
    }

    private static JsonWebKey ExportRsaPublicKey(string keyId, string algorithm, string serializedKey)
    {
        using var rsa = RSA.Create();
        rsa.ImportRSAPrivateKey(Convert.FromBase64String(serializedKey), out _);
        var parameters = rsa.ExportParameters(includePrivateParameters: false);

        return new JsonWebKey
        {
            Kid = keyId,
            Kty = "RSA",
            Use = "sig",
            Alg = algorithm,
            N = Base64Url.Encode(parameters.Modulus!),
            E = Base64Url.Encode(parameters.Exponent!),
        };
    }

    private static JsonWebKey ExportEcPublicKey(string keyId, string algorithm, string serializedKey)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportECPrivateKey(Convert.FromBase64String(serializedKey), out _);
        var parameters = ecdsa.ExportParameters(includePrivateParameters: false);

        return new JsonWebKey
        {
            Kid = keyId,
            Kty = "EC",
            Use = "sig",
            Alg = algorithm,
            Crv = "P-256",
            X = Base64Url.Encode(parameters.Q.X!),
            Y = Base64Url.Encode(parameters.Q.Y!),
        };
    }

}
