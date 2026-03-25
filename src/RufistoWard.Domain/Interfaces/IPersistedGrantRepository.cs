namespace RufistoWard.Identity.Interfaces;

using RufistoWard.Identity.Entities;

public interface IPersistedGrantRepository
{
    Task<PersistedGrant?> FindByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PersistedGrant>> FindBySubjectIdAsync(string subjectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PersistedGrant>> FindByTypeAsync(string type, CancellationToken cancellationToken = default);
    Task AddAsync(PersistedGrant grant, CancellationToken cancellationToken = default);
    Task UpdateAsync(PersistedGrant grant, CancellationToken cancellationToken = default);
    Task DeleteAsync(PersistedGrant grant, CancellationToken cancellationToken = default);
    Task DeleteExpiredAsync(CancellationToken cancellationToken = default);
}
