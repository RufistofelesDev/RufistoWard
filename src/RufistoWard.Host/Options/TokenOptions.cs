namespace RufistoWard.Core.Options;

using System.ComponentModel.DataAnnotations;

public sealed record TokenOptions
{
    [Required]
    public string Issuer { get; init; } = default!;

    [Range(60, 86400)]
    public int AccessTokenLifetimeSeconds { get; init; } = 3600;

    [Range(3600, 2592000)]
    public int RefreshTokenLifetimeSeconds { get; init; } = 86400;
}
