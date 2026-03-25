namespace RufistoWard.Auth.Protocol.Keys;

using RufistoWard.Identity.Entities;

public interface IKeyGenerationService
{
    Task<SigningKey> GenerateKeyAsync(string algorithm, CancellationToken cancellationToken = default);
}
