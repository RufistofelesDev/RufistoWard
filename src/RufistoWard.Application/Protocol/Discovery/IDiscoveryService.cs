namespace RufistoWard.Auth.Protocol.Discovery;

public interface IDiscoveryService
{
    Task<DiscoveryDocument> GetDiscoveryDocumentAsync(CancellationToken cancellationToken = default);
}
