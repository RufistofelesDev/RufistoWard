namespace RufistoWard.Tokens.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using RufistoWard.Identity.Entities;
using RufistoWard.Identity.Interfaces;

public sealed class UserRepository(RufistoWardDbContext context) : IUserRepository
{
    public async Task<User?> FindBySubjectIdAsync(string subjectId, CancellationToken cancellationToken = default)
        => await context.Users.FirstOrDefaultAsync(u => u.SubjectId == subjectId, cancellationToken);

    public async Task<User?> FindBySubjectIdWithClaimsAsync(string subjectId, CancellationToken cancellationToken = default)
        => await context.Users
            .Include(u => u.Claims)
            .FirstOrDefaultAsync(u => u.SubjectId == subjectId, cancellationToken);

    public async Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default)
        => await context.Users.FirstOrDefaultAsync(u => u.Username == username, cancellationToken);

    public async Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
        => await context.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
        => await context.Users.ToListAsync(cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await context.Users.AddAsync(user, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        context.Users.Update(user);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(User user, CancellationToken cancellationToken = default)
    {
        context.Users.Remove(user);
        await context.SaveChangesAsync(cancellationToken);
    }
}
