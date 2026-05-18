namespace RufistoWard.Tokens.Tests.Providers.KeyStorage;

using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using RufistoWard.Tokens.Providers.KeyStorage;

[Trait("Category", "Unit")]
public sealed class SigningKeyPersistenceRoundTripTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"rufistoward-test-{Guid.NewGuid():N}");

    private static IDataProtector CreateRealProtector() =>
        new EphemeralDataProtectionProvider().CreateProtector("RufistoWard.KeyStorage");

    [Fact]
    public async Task RealDataProtector_RoundTrips_RsaKey_AndProducesIdenticalSignature()
    {
        var ct = TestContext.Current.CancellationToken;
        var provider = new FileSystemKeyStorageProvider(_tempDir, CreateRealProtector());

        using var originalRsa = RSA.Create(2048);
        var keyMaterial = originalRsa.ExportPkcs8PrivateKey();
        const string keyId = "rsa-256-roundtrip";

        await provider.StoreKeyAsync(keyId, keyMaterial, ct);

        var loaded = await provider.LoadKeyAsync(keyId, ct);
        Assert.NotNull(loaded);
        Assert.Equal(keyMaterial, loaded);

        // RS256 (RSA-PKCS1-v1_5 + SHA-256) is deterministic, so the signature bytes
        // produced by the rehydrated key must be byte-identical to the original.
        using var rehydratedRsa = RSA.Create();
        rehydratedRsa.ImportPkcs8PrivateKey(loaded, bytesRead: out _);

        var payload = "the.quick.brown.fox.jumps.over.the.lazy.dog"u8.ToArray();
        var sigOriginal = originalRsa.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var sigRehydrated = rehydratedRsa.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        Assert.Equal(sigOriginal, sigRehydrated);
    }

    [Fact]
    public async Task TamperedBlob_LoadKey_SurfacesCryptographicException()
    {
        var ct = TestContext.Current.CancellationToken;
        var provider = new FileSystemKeyStorageProvider(_tempDir, CreateRealProtector());
        const string keyId = "tampered";

        await provider.StoreKeyAsync(keyId, [1, 2, 3, 4, 5], ct);

        var path = Path.Combine(_tempDir, $"{keyId}.key");
        var bytes = await File.ReadAllBytesAsync(path, ct);
        bytes[^1] ^= 0xFF;
        await File.WriteAllBytesAsync(path, bytes, ct);

        await Assert.ThrowsAsync<CryptographicException>(async () =>
            await provider.LoadKeyAsync(keyId, ct));
    }

    [Fact]
    public async Task StoredBlob_WithRealProtector_DoesNotContainPlaintext()
    {
        var ct = TestContext.Current.CancellationToken;
        var provider = new FileSystemKeyStorageProvider(_tempDir, CreateRealProtector());

        var keyMaterial = "RUFISTOWARD-PLAINTEXT-SENTINEL-DO-NOT-LEAK"u8.ToArray();
        const string keyId = "sentinel";

        await provider.StoreKeyAsync(keyId, keyMaterial, ct);

        var raw = await File.ReadAllBytesAsync(Path.Combine(_tempDir, $"{keyId}.key"), ct);

        Assert.False(
            ContainsSubsequence(raw, keyMaterial),
            "Protected on-disk blob unexpectedly contains plaintext key material — IDataProtector wiring is broken.");
    }

    private static bool ContainsSubsequence(byte[] haystack, byte[] needle)
    {
        if (needle.Length == 0 || haystack.Length < needle.Length)
        {
            return false;
        }

        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }
            if (match)
            {
                return true;
            }
        }
        return false;
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
