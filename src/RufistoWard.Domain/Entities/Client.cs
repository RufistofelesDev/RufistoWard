namespace RufistoWard.Identity.Entities;

using RufistoWard.Identity.Interfaces;
using RufistoWard.Identity.ValueObjects;

public sealed class Client : ITenantScoped
{
    public int Id { get; private set; }

    /// <summary>
    /// The tenant this row belongs to. Stamped on insert by the persistence layer.
    /// </summary>
    public int TenantId { get; private set; }
    public string ClientId { get; private set; } = default!;
    public string ClientName { get; private set; } = default!;
    public string? Description { get; private set; }
    public bool Enabled { get; private set; } = true;
    public bool RequirePkce { get; private set; } = true;
    public bool RequireClientSecret { get; private set; } = true;
    public int AccessTokenLifetimeSeconds { get; private set; } = 3600;
    public int RefreshTokenLifetimeSeconds { get; private set; } = 86400;
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private readonly List<ClientSecret> _secrets = [];
    public IReadOnlyCollection<ClientSecret> Secrets => _secrets.AsReadOnly();

    private readonly List<ClientScope> _scopes = [];
    public IReadOnlyCollection<ClientScope> Scopes => _scopes.AsReadOnly();

    private readonly List<ClientRedirectUri> _redirectUris = [];
    public IReadOnlyCollection<ClientRedirectUri> RedirectUris => _redirectUris.AsReadOnly();

    private readonly List<ClientGrantType> _grantTypes = [];
    public IReadOnlyCollection<ClientGrantType> GrantTypes => _grantTypes.AsReadOnly();

    private Client() { }

    public static Client Create(string clientId, string clientName, bool requireClientSecret = true)
    {
        return new Client
        {
            ClientId = clientId,
            ClientName = clientName,
            RequireClientSecret = requireClientSecret,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public void Update(string? clientName, bool? enabled, bool? requirePkce, int? accessTokenLifetimeSeconds, int? refreshTokenLifetimeSeconds)
    {
        if (clientName is not null)
            ClientName = clientName;
        if (enabled.HasValue)
            Enabled = enabled.Value;
        if (requirePkce.HasValue)
        {
            // Public clients (no client secret) have no other defense against authorization-code
            // interception, so PKCE is mandatory for them (OAuth 2.1 / Security BCP). Only
            // confidential clients may opt out.
            if (!requirePkce.Value && !RequireClientSecret)
            {
                throw new InvalidOperationException(
                    "PKCE cannot be disabled for a public client; only confidential clients (with a client secret) may opt out of PKCE.");
            }

            RequirePkce = requirePkce.Value;
        }
        if (accessTokenLifetimeSeconds.HasValue)
            AccessTokenLifetimeSeconds = accessTokenLifetimeSeconds.Value;
        if (refreshTokenLifetimeSeconds.HasValue)
            RefreshTokenLifetimeSeconds = refreshTokenLifetimeSeconds.Value;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddSecret(string hashedValue, string? description = null, DateTime? expiration = null)
    {
        _secrets.Add(new ClientSecret(hashedValue, description, expiration));
    }

    public void AddScope(string scope)
    {
        if (_scopes.All(s => s.Scope != scope))
            _scopes.Add(new ClientScope(scope));
    }

    /// <exception cref="ArgumentException">The URI is not absolute or uses a forbidden scheme.</exception>
    public void AddRedirectUri(string uri)
    {
        var validated = new RedirectUri(uri);
        if (_redirectUris.All(r => r.Uri != validated.Value))
            _redirectUris.Add(new ClientRedirectUri(validated.Value));
    }

    public void AddGrantType(string grantType)
    {
        if (_grantTypes.All(g => g.GrantType != grantType))
            _grantTypes.Add(new ClientGrantType(grantType));
    }
}
