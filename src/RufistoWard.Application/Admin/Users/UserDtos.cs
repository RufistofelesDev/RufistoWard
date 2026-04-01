namespace RufistoWard.Auth.Admin.Users;

using System.Text.Json.Serialization;

public sealed record CreateUserRequest
{
    [JsonPropertyName("username")]
    public string Username { get; init; } = default!;

    [JsonPropertyName("email")]
    public string Email { get; init; } = default!;

    [JsonPropertyName("password")]
    public string Password { get; init; } = default!;
}

public sealed record UpdateUserRequest
{
    [JsonPropertyName("username")]
    public string? Username { get; init; }

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("emailConfirmed")]
    public bool? EmailConfirmed { get; init; }
}

public sealed record UserResponse
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("subjectId")]
    public string SubjectId { get; init; } = default!;

    [JsonPropertyName("username")]
    public string Username { get; init; } = default!;

    [JsonPropertyName("email")]
    public string Email { get; init; } = default!;

    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }
}
