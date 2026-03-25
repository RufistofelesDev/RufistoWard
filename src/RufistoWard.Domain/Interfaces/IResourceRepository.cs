namespace RufistoWard.Identity.Interfaces;

using RufistoWard.Identity.Entities;

public interface IResourceRepository
{
    Task<ApiResource?> FindByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ApiResource>> FindByScopeNamesAsync(IEnumerable<string> scopeNames, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ApiResource>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(ApiResource resource, CancellationToken cancellationToken = default);
    Task UpdateAsync(ApiResource resource, CancellationToken cancellationToken = default);
    Task DeleteAsync(ApiResource resource, CancellationToken cancellationToken = default);
}
