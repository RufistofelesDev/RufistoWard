namespace RufistoWard.Tokens.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using RufistoWard.Identity.Entities;
using RufistoWard.Identity.Interfaces;

public sealed class PersistedGrantRepository(RufistoWardDbContext context) : IPersistedGrantRepository
{
    public async Task<PersistedGrant?> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        => await context.PersistedGrants.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

    public async Task<IReadOnlyList<PersistedGrant>> FindBySubjectIdAsync(string subjectId, CancellationToken cancellationToken = default)
        => await context.PersistedGrants
            .Where(g => g.SubjectId == subjectId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PersistedGrant>> FindByTypeAsync(string type, CancellationToken cancellationToken = default)
        => await context.PersistedGrants
            .Where(g => g.Type == type && g.ConsumedAt == null)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(PersistedGrant grant, CancellationToken cancellationToken = default)
    {
        await context.PersistedGrants.AddAsync(grant, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(PersistedGrant grant, CancellationToken cancellationToken = default)
    {
        context.PersistedGrants.Update(grant);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(PersistedGrant grant, CancellationToken cancellationToken = default)
    {
        context.PersistedGrants.Remove(grant);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteExpiredAsync(CancellationToken cancellationToken = default)
    {
        await context.PersistedGrants
            .Where(g => g.Expiration != null && g.Expiration < DateTime.UtcNow)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int> RevokeFamilyAsync(Guid familyId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await context.PersistedGrants
            .Where(g => g.FamilyId == familyId && g.ConsumedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.ConsumedAt, now), cancellationToken);
    }
}
