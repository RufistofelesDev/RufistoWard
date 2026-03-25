namespace RufistoWard.Auth.Protocol.Token;

using Microsoft.Extensions.Options;
using RufistoWard.Auth.Protocol.Common;
using RufistoWard.Auth.Protocol.Discovery;
using RufistoWard.Auth.Protocol.Validation;

public sealed class ClientCredentialsTokenHandler(
    IClientAuthenticator clientAuthenticator,
    ITokenBuilder tokenBuilder,
    IOptions<DiscoveryOptions> discoveryOptions) : ITokenRequestHandler
{
    public string GrantType => "client_credentials";

    public async Task<Result<TokenResponse>> HandleAsync(TokenRequest request, CancellationToken cancellationToken = default)
    {
        // Client credentials requires confidential client
        var clientResult = await clientAuthenticator.AuthenticateAsync(request.ClientId, request.ClientSecret, cancellationToken);
        if (!clientResult.IsSuccess)
        {
            return Result<TokenResponse>.Failure(clientResult.Error!);
        }

        var client = clientResult.Value!;

        // Client credentials requires a confidential client (must have secret)
        if (!client.RequireClientSecret)
        {
            return Result<TokenResponse>.Failure(
                ProtocolError.InvalidClient("Client credentials grant requires a confidential client."));
        }

        // Validate requested scopes against client's allowed scopes
        var requestedScopes = (request.Scope ?? "")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (requestedScopes.Length > 0)
        {
            var allowedScopes = client.Scopes.Select(s => s.Scope).ToHashSet();
            var invalidScopes = requestedScopes.Where(s => !allowedScopes.Contains(s)).ToList();
            if (invalidScopes.Count > 0)
            {
                return Result<TokenResponse>.Failure(
                    ProtocolError.InvalidScope($"Client is not allowed scope(s): {string.Join(", ", invalidScopes)}"));
            }
        }

        // Build access token — no user context for client_credentials
        var issuer = discoveryOptions.Value.Issuer.TrimEnd('/');
        var scopes = requestedScopes.Length > 0
            ? requestedScopes
            : client.Scopes.Select(s => s.Scope).ToArray();

        var tokenContext = new TokenBuildContext
        {
            Subject = client.ClientId, // client is the subject in M2M
            ClientId = client.ClientId,
            Issuer = issuer,
            LifetimeSeconds = client.AccessTokenLifetimeSeconds,
            Scopes = scopes,
        };

        var accessToken = await tokenBuilder.BuildAccessTokenAsync(tokenContext, cancellationToken);

        // No id_token or refresh_token for client_credentials
        return Result<TokenResponse>.Success(new TokenResponse
        {
            AccessToken = accessToken,
            ExpiresIn = client.AccessTokenLifetimeSeconds,
            Scope = string.Join(' ', scopes),
        });
    }
}
