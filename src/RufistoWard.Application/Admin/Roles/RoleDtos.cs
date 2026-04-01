namespace RufistoWard.Auth.Admin.Roles;

using System.Text.Json.Serialization;

public sealed record CreateRoleRequest
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = default!;

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("permissions")]
    public string[] Permissions { get; init; } = [];
}

public sealed record UpdateRoleRequest
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("permissions")]
    public string[]? Permissions { get; init; }
}

public sealed record RoleResponse
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = default!;

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("permissions")]
    public string[] Permissions { get; init; } = [];
}
