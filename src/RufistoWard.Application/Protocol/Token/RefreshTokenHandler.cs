namespace RufistoWard.Auth.Protocol.Token;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RufistoWard.Auth.Protocol.Common;
using RufistoWard.Auth.Protocol.Discovery;
using RufistoWard.Auth.Protocol.Validation;
using RufistoWard.Identity.Entities;
using RufistoWard.Identity.Interfaces;

public sealed class RefreshTokenHandler(
    IPersistedGrantRepository grantRepository,
    IClientAuthenticator clientAuthenticator,
    ITokenBuilder tokenBuilder,
    IOptions<DiscoveryOptions> discoveryOptions,
    ILogger<RefreshTokenHandler> logger) : ITokenRequestHandler
{
    public string GrantType => "refresh_token";

    public async Task<Result<TokenResponse>> HandleAsync(TokenRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Result<TokenResponse>.Failure(ProtocolError.InvalidRequest("refresh_token is required."));
        }

        // Authenticate client
        var clientResult = await clientAuthenticator.AuthenticateAsync(request.ClientId, request.ClientSecret, cancellationToken);
        if (!clientResult.IsSuccess)
        {
            return Result<TokenResponse>.Failure(clientResult.Error!);
        }

        var client = clientResult.Value!;

        // Find refresh token grant
        var grant = await grantRepository.FindByIdAsync(request.RefreshToken, cancellationToken);

        if (grant is null || grant.Type != "refresh_token")
        {
            return Result<TokenResponse>.Failure(ProtocolError.InvalidGrant("Invalid refresh token."));
        }

        if (grant.IsConsumed)
        {
            // Refresh-token reuse: a previously-consumed grant was presented again. Treat as replay
            // and revoke the entire originating family (RFC 6749 §10.4 / RFC 6819 §5.2.2.3) so a
            // stolen token cannot outlive its detection.
            var revokedCount = await grantRepository.RevokeFamilyAsync(grant.FamilyId, cancellationToken);
            logger.LogWarning(
                "Refresh token reuse detected: revoked {RevokedCount} grant(s) in family {FamilyId} (offending handle {GrantHandle}) at {Timestamp:o}.",
                revokedCount, grant.FamilyId, grant.Id, DateTime.UtcNow);
            return Result<TokenResponse>.Failure(ProtocolError.InvalidGrant("Refresh token has been revoked."));
        }

        if (grant.IsExpired)
        {
            return Result<TokenResponse>.Failure(ProtocolError.InvalidGrant("Refresh token has expired."));
        }

        // Validate client matches
        if (!string.Equals(grant.ClientId, client.ClientId, StringComparison.Ordinal))
        {
            return Result<TokenResponse>.Failure(ProtocolError.InvalidGrant("Client mismatch."));
        }

        // Parse stored scope
        var grantData = JsonSerializer.Deserialize<JsonElement>(grant.Data);
        var scope = grantData.TryGetProperty("scope", out var scopeProp)
            ? scopeProp.GetString() ?? ""
            : "";
        var scopes = scope.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Rotate: consume old token and create new one
        grant.MarkConsumed();
        await grantRepository.UpdateAsync(grant, cancellationToken);

        var newRefreshTokenId = Guid.NewGuid().ToString("N");
        var newRefreshGrant = PersistedGrant.Create(
            id: newRefreshTokenId,
            type: "refresh_token",
            clientId: client.ClientId,
            subjectId: grant.SubjectId,
            data: JsonSerializer.Serialize(new { scope }),
            expiration: DateTime.UtcNow.AddSeconds(client.RefreshTokenLifetimeSeconds),
            familyId: grant.FamilyId);
        await grantRepository.AddAsync(newRefreshGrant, cancellationToken);

        // Build tokens
        var issuer = discoveryOptions.Value.Issuer.TrimEnd('/');
        var tokenContext = new TokenBuildContext
        {
            Subject = grant.SubjectId ?? "",
            ClientId = client.ClientId,
            Issuer = issuer,
            LifetimeSeconds = client.AccessTokenLifetimeSeconds,
            Scopes = scopes,
            AuthTime = new DateTimeOffset(grant.CreatedAt, TimeSpan.Zero),
        };

        var accessToken = await tokenBuilder.BuildAccessTokenAsync(tokenContext, cancellationToken);

        string? idToken = null;
        if (scopes.Contains("openid"))
        {
            idToken = await tokenBuilder.BuildIdTokenAsync(tokenContext, accessToken, cancellationToken);
        }

        return Result<TokenResponse>.Success(new TokenResponse
        {
            AccessToken = accessToken,
            ExpiresIn = client.AccessTokenLifetimeSeconds,
            RefreshToken = newRefreshTokenId,
            IdToken = idToken,
            Scope = scope,
        });
    }
}
