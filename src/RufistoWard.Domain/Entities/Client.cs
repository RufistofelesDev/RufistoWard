namespace RufistoWard.Identity.Entities;

public sealed class Client
{
    public int Id { get; private set; }
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
}
