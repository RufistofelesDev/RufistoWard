namespace RufistoWard.Auth.Protocol.Validation;

using RufistoWard.Auth.Protocol.Common;
using RufistoWard.Identity.Entities;

public interface IClientAuthenticator
{
    Task<Result<Client>> AuthenticateAsync(
        string? clientId,
        string? clientSecret,
        CancellationToken cancellationToken = default);
}
