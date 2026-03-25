namespace RufistoWard.Tokens.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using RufistoWard.Identity.Entities;
using RufistoWard.Identity.Interfaces;

public sealed class ClientRepository(RufistoWardDbContext context) : IClientRepository
{
    public async Task<Client?> FindByClientIdAsync(string clientId, CancellationToken cancellationToken = default)
        => await context.Clients
            .Include(c => c.Secrets)
            .Include(c => c.Scopes)
            .Include(c => c.RedirectUris)
            .Include(c => c.GrantTypes)
            .FirstOrDefaultAsync(c => c.ClientId == clientId, cancellationToken);

    public async Task<IReadOnlyList<Client>> GetAllAsync(CancellationToken cancellationToken = default)
        => await context.Clients
            .Include(c => c.Scopes)
            .Include(c => c.GrantTypes)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Client client, CancellationToken cancellationToken = default)
    {
        await context.Clients.AddAsync(client, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Client client, CancellationToken cancellationToken = default)
    {
        context.Clients.Update(client);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Client client, CancellationToken cancellationToken = default)
    {
        context.Clients.Remove(client);
        await context.SaveChangesAsync(cancellationToken);
    }
}
