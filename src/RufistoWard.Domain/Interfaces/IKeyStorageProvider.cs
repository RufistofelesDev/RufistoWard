namespace RufistoWard.Identity.Interfaces;

public interface IKeyStorageProvider
{
    Task StoreKeyAsync(string keyId, byte[] keyMaterial, CancellationToken cancellationToken = default);
    Task<byte[]?> LoadKeyAsync(string keyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ListKeyIdsAsync(CancellationToken cancellationToken = default);
    Task DeleteKeyAsync(string keyId, CancellationToken cancellationToken = default);
}
