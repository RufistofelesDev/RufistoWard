namespace RufistoWard.Tokens.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using RufistoWard.Identity.Entities;
using RufistoWard.Identity.Interfaces;

public sealed class SigningKeyRepository(RufistoWardDbContext context) : ISigningKeyRepository
{
    public async Task<SigningKey?> FindByKeyIdAsync(string keyId, CancellationToken cancellationToken = default)
        => await context.SigningKeys.FirstOrDefaultAsync(k => k.KeyId == keyId, cancellationToken);

    public async Task<SigningKey?> GetActiveKeyAsync(CancellationToken cancellationToken = default)
        => await context.SigningKeys
            .Where(k => k.IsActive)
            .OrderByDescending(k => k.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<SigningKey>> GetAllAsync(CancellationToken cancellationToken = default)
        => await context.SigningKeys.ToListAsync(cancellationToken);

    public async Task AddAsync(SigningKey key, CancellationToken cancellationToken = default)
    {
        await context.SigningKeys.AddAsync(key, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(SigningKey key, CancellationToken cancellationToken = default)
    {
        context.SigningKeys.Update(key);
        await context.SaveChangesAsync(cancellationToken);
    }
}
