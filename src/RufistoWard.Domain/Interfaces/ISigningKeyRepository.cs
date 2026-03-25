namespace RufistoWard.Identity.Interfaces;

using RufistoWard.Identity.Entities;

public interface ISigningKeyRepository
{
    Task<SigningKey?> FindByKeyIdAsync(string keyId, CancellationToken cancellationToken = default);
    Task<SigningKey?> GetActiveKeyAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SigningKey>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(SigningKey key, CancellationToken cancellationToken = default);
    Task UpdateAsync(SigningKey key, CancellationToken cancellationToken = default);
}
