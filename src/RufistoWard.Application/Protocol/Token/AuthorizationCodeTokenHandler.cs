namespace RufistoWard.Auth.Protocol.Token;

using System.Text.Json;
using Microsoft.Extensions.Options;
using RufistoWard.Auth.Protocol.Common;
using RufistoWard.Auth.Protocol.Discovery;
using RufistoWard.Auth.Protocol.Validation;
using RufistoWard.Identity.Entities;
using RufistoWard.Identity.Interfaces;

public sealed class AuthorizationCodeTokenHandler(
    IPersistedGrantRepository grantRepository,
    IClientAuthenticator clientAuthenticator,
    ITokenBuilder tokenBuilder,
    IOptions<DiscoveryOptions> discoveryOptions) : ITokenRequestHandler
{
    public string GrantType => "authorization_code";

    public async Task<Result<TokenResponse>> HandleAsync(TokenRequest request, CancellationToken cancellationToken = default)
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return Result<TokenResponse>.Failure(ProtocolError.InvalidRequest("code is required."));
        }

        if (string.IsNullOrWhiteSpace(request.RedirectUri))
        {
            return Result<TokenResponse>.Failure(ProtocolError.InvalidRequest("redirect_uri is required."));
        }

        // Authenticate client
        var clientResult = await clientAuthenticator.AuthenticateAsync(request.ClientId, request.ClientSecret, cancellationToken);
        if (!clientResult.IsSuccess)
        {
            return Result<TokenResponse>.Failure(clientResult.Error!);
        }

        var client = clientResult.Value!;

        // Find authorization code
        var grant = await grantRepository.FindByIdAsync(request.Code, cancellationToken);

        if (grant is null || grant.Type != "authorization_code")
        {
            return Result<TokenResponse>.Failure(ProtocolError.InvalidGrant("Invalid authorization code."));
        }

        // Check if code was already consumed (replay attack)
        if (grant.IsConsumed)
        {
            // Revoke all tokens for this subject+client (FR14)
            await RevokeAllGrantsAsync(grant.SubjectId, grant.ClientId, cancellationToken);
            return Result<TokenResponse>.Failure(ProtocolError.InvalidGrant("Authorization code has already been used."));
        }

        // Check expiration
        if (grant.IsExpired)
        {
            return Result<TokenResponse>.Failure(ProtocolError.InvalidGrant("Authorization code has expired."));
        }

        // Deserialize code data
        var codeData = JsonSerializer.Deserialize<AuthorizationCodeData>(grant.Data);
        if (codeData is null)
        {
            return Result<TokenResponse>.Failure(ProtocolError.InvalidGrant("Invalid authorization code data."));
        }

        // Validate client_id matches
        if (!string.Equals(codeData.ClientId, client.ClientId, StringComparison.Ordinal))
        {
            return Result<TokenResponse>.Failure(ProtocolError.InvalidGrant("Client mismatch."));
        }

        // Validate redirect_uri matches
        if (!string.Equals(codeData.RedirectUri, request.RedirectUri, StringComparison.Ordinal))
        {
            return Result<TokenResponse>.Failure(ProtocolError.InvalidGrant("redirect_uri mismatch."));
        }

        // Validate PKCE
        if (!string.IsNullOrEmpty(codeData.CodeChallenge))
        {
            if (string.IsNullOrWhiteSpace(request.CodeVerifier))
            {
                return Result<TokenResponse>.Failure(ProtocolError.InvalidGrant("code_verifier is required."));
            }

            if (!PkceValidator.ValidateS256(request.CodeVerifier, codeData.CodeChallenge))
            {
                return Result<TokenResponse>.Failure(ProtocolError.InvalidGrant("PKCE validation failed."));
            }
        }

        // Mark code as consumed
        grant.MarkConsumed();
        await grantRepository.UpdateAsync(grant, cancellationToken);

        // Build tokens
        var issuer = discoveryOptions.Value.Issuer.TrimEnd('/');
        var scopes = codeData.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var tokenContext = new TokenBuildContext
        {
            Subject = codeData.SubjectId,
            ClientId = client.ClientId,
            Issuer = issuer,
            LifetimeSeconds = client.AccessTokenLifetimeSeconds,
            Scopes = scopes,
            Nonce = codeData.Nonce,
        };

        var accessToken = await tokenBuilder.BuildAccessTokenAsync(tokenContext, cancellationToken);

        string? idToken = null;
        if (scopes.Contains("openid"))
        {
            idToken = await tokenBuilder.BuildIdTokenAsync(tokenContext, accessToken, cancellationToken);
        }

        // Create refresh token
        var refreshTokenId = Guid.NewGuid().ToString("N");
        var refreshGrant = PersistedGrant.Create(
            id: refreshTokenId,
            type: "refresh_token",
            clientId: client.ClientId,
            subjectId: codeData.SubjectId,
            data: JsonSerializer.Serialize(new { scope = codeData.Scope }),
            expiration: DateTime.UtcNow.AddSeconds(client.RefreshTokenLifetimeSeconds));
        await grantRepository.AddAsync(refreshGrant, cancellationToken);

        return Result<TokenResponse>.Success(new TokenResponse
        {
            AccessToken = accessToken,
            ExpiresIn = client.AccessTokenLifetimeSeconds,
            RefreshToken = refreshTokenId,
            IdToken = idToken,
            Scope = codeData.Scope,
        });
    }

    private async Task RevokeAllGrantsAsync(string? subjectId, string clientId, CancellationToken cancellationToken)
    {
        if (subjectId is null)
        {
            return;
        }

        var grants = await grantRepository.FindBySubjectIdAsync(subjectId, cancellationToken);
        foreach (var g in grants.Where(g => g.ClientId == clientId && !g.IsConsumed))
        {
            g.MarkConsumed();
            await grantRepository.UpdateAsync(g, cancellationToken);
        }
    }
}
