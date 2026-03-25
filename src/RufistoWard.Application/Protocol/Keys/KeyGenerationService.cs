namespace RufistoWard.Auth.Protocol.Keys;

using System.Security.Cryptography;
using RufistoWard.Identity.Entities;
using RufistoWard.Identity.Interfaces;

public sealed class KeyGenerationService(ISigningKeyRepository signingKeyRepository) : IKeyGenerationService
{
    public async Task<SigningKey> GenerateKeyAsync(string algorithm, CancellationToken cancellationToken = default)
    {
        var (keyType, serializedKey) = algorithm.ToUpperInvariant() switch
        {
            "RS256" => GenerateRsaKey(),
            "ES256" => GenerateEcKey(),
            _ => throw new ArgumentException($"Unsupported signing algorithm: {algorithm}", nameof(algorithm)),
        };

        var signingKey = SigningKey.Create(
            keyId: Guid.NewGuid().ToString("N")[..16],
            algorithm: algorithm,
            keyType: keyType,
            serializedKey: serializedKey);

        await signingKeyRepository.AddAsync(signingKey, cancellationToken);
        return signingKey;
    }

    private static (string KeyType, string SerializedKey) GenerateRsaKey()
    {
        using var rsa = RSA.Create(2048);
        var privateKey = rsa.ExportRSAPrivateKey();
        return ("RSA", Convert.ToBase64String(privateKey));
    }

    private static (string KeyType, string SerializedKey) GenerateEcKey()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var privateKey = ecdsa.ExportECPrivateKey();
        return ("EC", Convert.ToBase64String(privateKey));
    }
}
