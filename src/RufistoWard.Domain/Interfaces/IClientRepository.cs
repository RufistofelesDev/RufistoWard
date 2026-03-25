namespace RufistoWard.Identity.Interfaces;

using RufistoWard.Identity.Entities;

public interface IClientRepository
{
    Task<Client?> FindByClientIdAsync(string clientId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Client>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Client client, CancellationToken cancellationToken = default);
    Task UpdateAsync(Client client, CancellationToken cancellationToken = default);
    Task DeleteAsync(Client client, CancellationToken cancellationToken = default);
}
