namespace RufistoWard.Tokens.Providers.KeyStorage;

using System.Security.Cryptography.X509Certificates;
using RufistoWard.Identity.Interfaces;

public sealed class CertificateStoreKeyStorageProvider : IKeyStorageProvider
{
    public Task StoreKeyAsync(string keyId, byte[] keyMaterial, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            "CertificateStoreKeyStorageProvider is read-only. Keys must be managed externally in the Windows Certificate Store.");
    }

    public Task<byte[]?> LoadKeyAsync(string keyId, CancellationToken cancellationToken = default)
    {
        using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadOnly);

        var cert = store.Certificates
            .Find(X509FindType.FindByThumbprint, keyId, validOnly: false)
            .OfType<X509Certificate2>()
            .FirstOrDefault();

        if (cert?.HasPrivateKey != true)
        {
            return Task.FromResult<byte[]?>(null);
        }

        var privateKey = cert.GetRSAPrivateKey()?.ExportRSAPrivateKey()
            ?? cert.GetECDsaPrivateKey()?.ExportECPrivateKey();

        return Task.FromResult(privateKey);
    }

    public Task<IReadOnlyList<string>> ListKeyIdsAsync(CancellationToken cancellationToken = default)
    {
        using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadOnly);

        var thumbprints = store.Certificates
            .OfType<X509Certificate2>()
            .Where(c => c.HasPrivateKey)
            .Select(c => c.Thumbprint)
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(thumbprints);
    }

    public Task DeleteKeyAsync(string keyId, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            "CertificateStoreKeyStorageProvider is read-only. Keys must be managed externally.");
    }
}
