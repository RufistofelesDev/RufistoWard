namespace RufistoWard.Tokens.Tests.Providers;

using Microsoft.AspNetCore.DataProtection;
using RufistoWard.Tokens.Providers.KeyStorage;

[Trait("Category", "Unit")]
public sealed class FileSystemKeyStorageProviderTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"rufistoward-test-{Guid.NewGuid():N}");

    private static IDataProtector CreateTestProtector() => new TestDataProtector();

    /// <summary>
    /// Simple reversible XOR-based protector for unit tests only.
    /// </summary>
    private sealed class TestDataProtector : IDataProtector
    {
        private const byte Key = 0xAB;
        public IDataProtector CreateProtector(string purpose) => this;
        public byte[] Protect(byte[] plaintext) => plaintext.Select(b => (byte)(b ^ Key)).ToArray();
        public byte[] Unprotect(byte[] protectedData) => protectedData.Select(b => (byte)(b ^ Key)).ToArray();
    }

    [Fact]
    public async Task StoreAndLoad_RoundTrips()
    {
        var ct = TestContext.Current.CancellationToken;
        var provider = new FileSystemKeyStorageProvider(_tempDir, CreateTestProtector());
        var keyData = new byte[] { 1, 2, 3, 4, 5 };

        await provider.StoreKeyAsync("test-key", keyData, ct);
        var loaded = await provider.LoadKeyAsync("test-key", ct);

        Assert.NotNull(loaded);
        Assert.Equal(keyData, loaded);
    }

    [Fact]
    public async Task LoadKey_NonExistent_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var provider = new FileSystemKeyStorageProvider(_tempDir, CreateTestProtector());

        var result = await provider.LoadKeyAsync("nonexistent", ct);

        Assert.Null(result);
    }

    [Fact]
    public async Task ListKeyIds_ReturnsStoredKeys()
    {
        var ct = TestContext.Current.CancellationToken;
        var provider = new FileSystemKeyStorageProvider(_tempDir, CreateTestProtector());

        await provider.StoreKeyAsync("key-1", [1, 2, 3], ct);
        await provider.StoreKeyAsync("key-2", [4, 5, 6], ct);

        var ids = await provider.ListKeyIdsAsync(ct);

        Assert.Equal(2, ids.Count);
        Assert.Contains("key-1", ids);
        Assert.Contains("key-2", ids);
    }

    [Fact]
    public async Task DeleteKey_RemovesKeyFile()
    {
        var ct = TestContext.Current.CancellationToken;
        var provider = new FileSystemKeyStorageProvider(_tempDir, CreateTestProtector());

        await provider.StoreKeyAsync("to-delete", [1, 2, 3], ct);
        await provider.DeleteKeyAsync("to-delete", ct);

        var result = await provider.LoadKeyAsync("to-delete", ct);
        Assert.Null(result);
    }

    [Fact]
    public async Task StoredData_IsEncrypted_NotPlaintext()
    {
        var ct = TestContext.Current.CancellationToken;
        var provider = new FileSystemKeyStorageProvider(_tempDir, CreateTestProtector());
        var keyData = new byte[] { 1, 2, 3, 4, 5 };

        await provider.StoreKeyAsync("encrypted-key", keyData, ct);

        // Read raw file — it should NOT match the original plaintext
        var rawBytes = await File.ReadAllBytesAsync(Path.Combine(_tempDir, "encrypted-key.key"), ct);
        Assert.NotEqual(keyData, rawBytes);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
