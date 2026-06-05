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

    /// <summary>
    /// Revokes every unconsumed grant in the given family in a single SQL UPDATE
    /// (sets <c>ConsumedAt</c>). Returns the number of grants revoked; a second call
    /// for the same family is a no-op (returns 0). See RFC 6819 §5.2.2.3.
    /// </summary>
    Task<int> RevokeFamilyAsync(Guid familyId, CancellationToken cancellationToken = default);
}
