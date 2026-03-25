namespace RufistoWard.Auth.Admin.Clients;

using System.Text.Json.Serialization;

public sealed record CreateClientRequest
{
    [JsonPropertyName("clientName")]
    public string ClientName { get; init; } = default!;

    [JsonPropertyName("grantTypes")]
    public string[] GrantTypes { get; init; } = [];

    [JsonPropertyName("redirectUris")]
    public string[] RedirectUris { get; init; } = [];

    [JsonPropertyName("allowedScopes")]
    public string[] AllowedScopes { get; init; } = [];

    [JsonPropertyName("requireClientSecret")]
    public bool RequireClientSecret { get; init; } = true;
}

public sealed record UpdateClientRequest
{
    [JsonPropertyName("clientName")]
    public string? ClientName { get; init; }

    [JsonPropertyName("enabled")]
    public bool? Enabled { get; init; }

    [JsonPropertyName("requirePkce")]
    public bool? RequirePkce { get; init; }

    [JsonPropertyName("accessTokenLifetimeSeconds")]
    public int? AccessTokenLifetimeSeconds { get; init; }

    [JsonPropertyName("refreshTokenLifetimeSeconds")]
    public int? RefreshTokenLifetimeSeconds { get; init; }
}

public sealed record ClientResponse
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("clientId")]
    public string ClientId { get; init; } = default!;

    [JsonPropertyName("clientName")]
    public string ClientName { get; init; } = default!;

    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }

    [JsonPropertyName("grantTypes")]
    public string[] GrantTypes { get; init; } = [];

    [JsonPropertyName("redirectUris")]
    public string[] RedirectUris { get; init; } = [];

    [JsonPropertyName("allowedScopes")]
    public string[] AllowedScopes { get; init; } = [];

    [JsonPropertyName("clientSecret")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ClientSecret { get; init; }
}
