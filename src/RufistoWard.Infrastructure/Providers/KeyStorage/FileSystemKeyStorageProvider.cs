namespace RufistoWard.Tokens.Providers.KeyStorage;

using RufistoWard.Identity.Interfaces;

public sealed class FileSystemKeyStorageProvider(string keyDirectory) : IKeyStorageProvider
{
    public async Task StoreKeyAsync(string keyId, byte[] keyMaterial, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(keyDirectory);
        var path = GetKeyPath(keyId);
        await File.WriteAllBytesAsync(path, keyMaterial, cancellationToken);
    }

    public async Task<byte[]?> LoadKeyAsync(string keyId, CancellationToken cancellationToken = default)
    {
        var path = GetKeyPath(keyId);
        if (!File.Exists(path))
        {
            return null;
        }
        return await File.ReadAllBytesAsync(path, cancellationToken);
    }

    public Task<IReadOnlyList<string>> ListKeyIdsAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(keyDirectory))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        var keyIds = Directory.GetFiles(keyDirectory, "*.key")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name is not null)
            .Cast<string>()
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(keyIds);
    }

    public Task DeleteKeyAsync(string keyId, CancellationToken cancellationToken = default)
    {
        var path = GetKeyPath(keyId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        return Task.CompletedTask;
    }

    private string GetKeyPath(string keyId) => Path.Combine(keyDirectory, $"{keyId}.key");
}
