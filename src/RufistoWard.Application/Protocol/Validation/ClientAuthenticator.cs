namespace RufistoWard.Auth.Protocol.Validation;

using RufistoWard.Auth.Protocol.Common;
using RufistoWard.Identity.Entities;
using RufistoWard.Identity.Interfaces;

public sealed class ClientAuthenticator(IClientRepository clientRepository) : IClientAuthenticator
{
    public async Task<Result<Client>> AuthenticateAsync(
        string? clientId,
        string? clientSecret,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return Result<Client>.Failure(ProtocolError.InvalidClient("client_id is required."));
        }

        var client = await clientRepository.FindByClientIdAsync(clientId, cancellationToken);

        if (client is null || !client.Enabled)
        {
            return Result<Client>.Failure(ProtocolError.InvalidClient("Client authentication failed."));
        }

        if (client.RequireClientSecret)
        {
            if (string.IsNullOrWhiteSpace(clientSecret))
            {
                return Result<Client>.Failure(ProtocolError.InvalidClient("client_secret is required for this client."));
            }

            // For MVP, verify secret exists in client's secrets collection
            // Full hash verification will be added with ISecretHasher
            var hasValidSecret = client.Secrets.Any(s =>
                s.Value == clientSecret &&
                (s.Expiration is null || s.Expiration > DateTime.UtcNow));

            if (!hasValidSecret)
            {
                return Result<Client>.Failure(ProtocolError.InvalidClient("Client authentication failed."));
            }
        }

        return Result<Client>.Success(client);
    }
}
