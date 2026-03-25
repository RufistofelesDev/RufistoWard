namespace RufistoWard.Auth.Protocol.Discovery;

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

public sealed class DiscoveryService(
    IOptions<DiscoveryOptions> options,
    HybridCache cache) : IDiscoveryService
{
    public async Task<DiscoveryDocument> GetDiscoveryDocumentAsync(CancellationToken cancellationToken = default)
    {
        return await cache.GetOrCreateAsync(
            "oidc:discovery-document",
            _ => new ValueTask<DiscoveryDocument>(BuildDiscoveryDocument()),
            cancellationToken: cancellationToken);
    }

    private DiscoveryDocument BuildDiscoveryDocument()
    {
        var issuer = options.Value.Issuer.TrimEnd('/');

        return new DiscoveryDocument
        {
            Issuer = issuer,
            AuthorizationEndpoint = $"{issuer}/connect/authorize",
            TokenEndpoint = $"{issuer}/connect/token",
            UserinfoEndpoint = $"{issuer}/connect/userinfo",
            JwksUri = $"{issuer}/.well-known/jwks",
            EndSessionEndpoint = $"{issuer}/connect/endsession",
            RevocationEndpoint = $"{issuer}/connect/revocation",
            IntrospectionEndpoint = $"{issuer}/connect/introspect",
            DeviceAuthorizationEndpoint = $"{issuer}/connect/device",
            ResponseTypesSupported = ["code"],
            GrantTypesSupported =
            [
                "authorization_code",
                "client_credentials",
                "refresh_token",
                "urn:ietf:params:oauth:grant-type:device_code",
            ],
            SubjectTypesSupported = ["public"],
            IdTokenSigningAlgValuesSupported = ["RS256"],
            ScopesSupported = ["openid", "profile", "email"],
            TokenEndpointAuthMethodsSupported = ["client_secret_post", "client_secret_basic"],
            ClaimsSupported = ["sub", "name", "email", "email_verified", "role"],
            CodeChallengeMethodsSupported = ["S256"],
        };
    }
}
