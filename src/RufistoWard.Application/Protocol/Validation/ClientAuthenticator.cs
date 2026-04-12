namespace RufistoWard.Auth.Protocol.Validation;

using System.Security.Cryptography;
using System.Text;
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

            var incomingHash = Convert.ToBase64String(
                SHA256.HashData(Encoding.UTF8.GetBytes(clientSecret)));

            var hasValidSecret = client.Secrets.Any(s =>
                (s.Expiration is null || s.Expiration > DateTime.UtcNow) &&
                CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(s.Value),
                    Encoding.UTF8.GetBytes(incomingHash)));

            if (!hasValidSecret)
            {
                return Result<Client>.Failure(ProtocolError.InvalidClient("Client authentication failed."));
            }
        }

        return Result<Client>.Success(client);
    }
}
