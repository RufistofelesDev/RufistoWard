namespace RufistoWard.Identity.Interfaces;

using RufistoWard.Identity.Entities;

public interface IUserRepository
{
    Task<User?> FindBySubjectIdAsync(string subjectId, CancellationToken cancellationToken = default);
    Task<User?> FindBySubjectIdWithClaimsAsync(string subjectId, CancellationToken cancellationToken = default);
    Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);
    Task DeleteAsync(User user, CancellationToken cancellationToken = default);
}
