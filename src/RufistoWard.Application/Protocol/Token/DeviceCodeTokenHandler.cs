namespace RufistoWard.Auth.Protocol.Token;

using System.Text.Json;
using Microsoft.Extensions.Options;
using RufistoWard.Auth.Protocol.Common;
using RufistoWard.Auth.Protocol.Device;
using RufistoWard.Auth.Protocol.Discovery;
using RufistoWard.Auth.Protocol.Validation;
using RufistoWard.Identity.Entities;
using RufistoWard.Identity.Interfaces;

public sealed class DeviceCodeTokenHandler(
    IPersistedGrantRepository grantRepository,
    IClientAuthenticator clientAuthenticator,
    ITokenBuilder tokenBuilder,
    IOptions<DiscoveryOptions> discoveryOptions) : ITokenRequestHandler
{
    public string GrantType => "urn:ietf:params:oauth:grant-type:device_code";

    public async Task<Result<TokenResponse>> HandleAsync(TokenRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return Result<TokenResponse>.Failure(ProtocolError.InvalidRequest("device_code is required."));
        }

        // Authenticate client
        var clientResult = await clientAuthenticator.AuthenticateAsync(request.ClientId, request.ClientSecret, cancellationToken);
        if (!clientResult.IsSuccess)
        {
            return Result<TokenResponse>.Failure(clientResult.Error!);
        }

        var client = clientResult.Value!;

        // Find device code grant
        var grant = await grantRepository.FindByIdAsync(request.Code, cancellationToken);
        if (grant is null || grant.Type != "device_code")
        {
            return Result<TokenResponse>.Failure(ProtocolError.InvalidGrant("Invalid device code."));
        }

        if (grant.IsExpired)
        {
            return Result<TokenResponse>.Failure(new ProtocolError("expired_token", "The device code has expired."));
        }

        if (grant.IsConsumed)
        {
            return Result<TokenResponse>.Failure(ProtocolError.InvalidGrant("Device code has already been used."));
        }

        var codeData = JsonSerializer.Deserialize<DeviceCodeData>(grant.Data);
        if (codeData is null)
        {
            return Result<TokenResponse>.Failure(ProtocolError.InvalidGrant("Invalid device code data."));
        }

        // Check if denied
        if (codeData.Denied)
        {
            return Result<TokenResponse>.Failure(new ProtocolError("access_denied", "The user denied the authorization request."));
        }

        // Check slow_down (poll interval = 5 seconds)
        if (codeData.LastPolled.HasValue && (DateTime.UtcNow - codeData.LastPolled.Value).TotalSeconds < 5)
        {
            return Result<TokenResponse>.Failure(new ProtocolError("slow_down", "Polling too frequently."));
        }

        // Update last polled time
        codeData.LastPolled = DateTime.UtcNow;

        // Check if not yet approved
        if (!codeData.Approved || string.IsNullOrEmpty(codeData.SubjectId))
        {
            grant.UpdateData(JsonSerializer.Serialize(codeData));
            await grantRepository.UpdateAsync(grant, cancellationToken);

            return Result<TokenResponse>.Failure(new ProtocolError("authorization_pending", "The user has not yet authorized the device."));
        }

        // Approved — issue tokens and consume the grant
        grant.MarkConsumed();
        await grantRepository.UpdateAsync(grant, cancellationToken);

        var issuer = discoveryOptions.Value.Issuer.TrimEnd('/');
        var scopes = codeData.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var tokenContext = new TokenBuildContext
        {
            Subject = codeData.SubjectId,
            ClientId = client.ClientId,
            Issuer = issuer,
            LifetimeSeconds = client.AccessTokenLifetimeSeconds,
            Scopes = scopes,
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
}
