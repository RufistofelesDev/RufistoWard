namespace RufistoWard.Identity.Interfaces;

using RufistoWard.Identity.Entities;

public interface IScopeRepository
{
    Task<ApiScope?> FindByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ApiScope>> FindByNamesAsync(IEnumerable<string> names, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ApiScope>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(ApiScope scope, CancellationToken cancellationToken = default);
    Task UpdateAsync(ApiScope scope, CancellationToken cancellationToken = default);
    Task DeleteAsync(ApiScope scope, CancellationToken cancellationToken = default);
}
