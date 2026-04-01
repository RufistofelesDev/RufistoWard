namespace RufistoWard.Tokens.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using RufistoWard.Identity.Entities;
using RufistoWard.Identity.Interfaces;

public sealed class ScopeRepository(RufistoWardDbContext context) : IScopeRepository
{
    public async Task<ApiScope?> FindByNameAsync(string name, CancellationToken cancellationToken = default)
        => await context.ApiScopes.FirstOrDefaultAsync(s => s.Name == name, cancellationToken);

    public async Task<IReadOnlyList<ApiScope>> FindByNamesAsync(IEnumerable<string> names, CancellationToken cancellationToken = default)
    {
        var nameList = names.ToList();
        return await context.ApiScopes
            .Where(s => nameList.Contains(s.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApiScope>> GetAllAsync(CancellationToken cancellationToken = default)
        => await context.ApiScopes.ToListAsync(cancellationToken);

    public async Task AddAsync(ApiScope scope, CancellationToken cancellationToken = default)
    {
        await context.ApiScopes.AddAsync(scope, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(ApiScope scope, CancellationToken cancellationToken = default)
    {
        context.ApiScopes.Update(scope);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(ApiScope scope, CancellationToken cancellationToken = default)
    {
        context.ApiScopes.Remove(scope);
        await context.SaveChangesAsync(cancellationToken);
    }
}
