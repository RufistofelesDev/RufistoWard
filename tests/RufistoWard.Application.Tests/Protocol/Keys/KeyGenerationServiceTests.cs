namespace RufistoWard.Auth.Tests.Protocol.Keys;

using System.Security.Cryptography;
using RufistoWard.Auth.Protocol.Keys;
using RufistoWard.Identity.Entities;
using RufistoWard.Identity.Interfaces;

[Trait("Category", "Unit")]
public sealed class KeyGenerationServiceTests
{
    [Fact]
    public async Task GenerateKey_RS256_CreatesValidRsaKey()
    {
        var ct = TestContext.Current.CancellationToken;
        var repo = new InMemorySigningKeyRepository();
        var service = new KeyGenerationService(repo);

        var key = await service.GenerateKeyAsync("RS256", ct);

        Assert.Equal("RSA", key.KeyType);
        Assert.Equal("RS256", key.Algorithm);
        Assert.True(key.IsActive);
        Assert.NotEmpty(key.KeyId);
        Assert.NotEmpty(key.SerializedKey);

        // Verify key material is valid RSA
        using var rsa = RSA.Create();
        rsa.ImportRSAPrivateKey(Convert.FromBase64String(key.SerializedKey), out _);
        Assert.True(rsa.KeySize >= 2048);
    }

    [Fact]
    public async Task GenerateKey_ES256_CreatesValidEcKey()
    {
        var ct = TestContext.Current.CancellationToken;
        var repo = new InMemorySigningKeyRepository();
        var service = new KeyGenerationService(repo);

        var key = await service.GenerateKeyAsync("ES256", ct);

        Assert.Equal("EC", key.KeyType);
        Assert.Equal("ES256", key.Algorithm);
        Assert.True(key.IsActive);

        // Verify key material is valid EC
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportECPrivateKey(Convert.FromBase64String(key.SerializedKey), out _);
        Assert.Equal(256, ecdsa.KeySize);
    }

    [Fact]
    public async Task GenerateKey_UnsupportedAlgorithm_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        var repo = new InMemorySigningKeyRepository();
        var service = new KeyGenerationService(repo);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GenerateKeyAsync("PS256", ct));
    }

    [Fact]
    public async Task GenerateKey_StoresInRepository()
    {
        var ct = TestContext.Current.CancellationToken;
        var repo = new InMemorySigningKeyRepository();
        var service = new KeyGenerationService(repo);

        await service.GenerateKeyAsync("RS256", ct);

        var keys = await repo.GetAllAsync(ct);
        Assert.Single(keys);
    }

    private sealed class InMemorySigningKeyRepository : ISigningKeyRepository
    {
        private readonly List<SigningKey> _keys = [];

        public Task<SigningKey?> FindByKeyIdAsync(string keyId, CancellationToken cancellationToken = default)
            => Task.FromResult(_keys.FirstOrDefault(k => k.KeyId == keyId));

        public Task<SigningKey?> GetActiveKeyAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_keys.FirstOrDefault(k => k.IsActive));

        public Task<IReadOnlyList<SigningKey>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SigningKey>>(_keys.ToList());

        public Task AddAsync(SigningKey key, CancellationToken cancellationToken = default)
        {
            _keys.Add(key);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(SigningKey key, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
