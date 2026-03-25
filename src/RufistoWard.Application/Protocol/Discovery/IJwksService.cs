namespace RufistoWard.Auth.Protocol.Discovery;

public interface IJwksService
{
    Task<JwksDocument> GetJwksDocumentAsync(CancellationToken cancellationToken = default);
}
