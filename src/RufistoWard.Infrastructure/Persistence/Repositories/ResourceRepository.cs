namespace RufistoWard.Tokens.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using RufistoWard.Identity.Entities;
using RufistoWard.Identity.Interfaces;

public sealed class ResourceRepository(RufistoWardDbContext context) : IResourceRepository
{
    public async Task<ApiResource?> FindByNameAsync(string name, CancellationToken cancellationToken = default)
        => await context.ApiResources
            .Include(r => r.Scopes)
            .FirstOrDefaultAsync(r => r.Name == name, cancellationToken);

    public async Task<IReadOnlyList<ApiResource>> FindByScopeNamesAsync(IEnumerable<string> scopeNames, CancellationToken cancellationToken = default)
    {
        var scopeList = scopeNames.ToList();
        return await context.ApiResources
            .Include(r => r.Scopes)
            .Where(r => r.Scopes.Any(s => scopeList.Contains(s.Scope)))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApiResource>> GetAllAsync(CancellationToken cancellationToken = default)
        => await context.ApiResources
            .Include(r => r.Scopes)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(ApiResource resource, CancellationToken cancellationToken = default)
    {
        await context.ApiResources.AddAsync(resource, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(ApiResource resource, CancellationToken cancellationToken = default)
    {
        context.ApiResources.Update(resource);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(ApiResource resource, CancellationToken cancellationToken = default)
    {
        context.ApiResources.Remove(resource);
        await context.SaveChangesAsync(cancellationToken);
    }
}
