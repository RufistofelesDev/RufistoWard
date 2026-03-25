namespace RufistoWard.Identity.Interfaces;

using RufistoWard.Identity.Entities;

public interface IRoleRepository
{
    Task<Role?> FindByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Role role, CancellationToken cancellationToken = default);
    Task UpdateAsync(Role role, CancellationToken cancellationToken = default);
    Task DeleteAsync(Role role, CancellationToken cancellationToken = default);
}
