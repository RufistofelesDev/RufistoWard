namespace RufistoWard.Core.Options;

using System.ComponentModel.DataAnnotations;

public sealed record RateLimitOptions
{
    [Range(1, 10000)]
    public int TokenEndpointPermitLimit { get; init; } = 100;

    [Range(1, 3600)]
    public int TokenEndpointWindowSeconds { get; init; } = 60;
}
