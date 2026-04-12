namespace RufistoWard.Auth.Protocol.Token;

public interface ITokenBuilder
{
    Task<string> BuildAccessTokenAsync(TokenBuildContext context, CancellationToken cancellationToken = default);
    Task<string> BuildIdTokenAsync(TokenBuildContext context, string accessToken, CancellationToken cancellationToken = default);
}

public sealed record TokenBuildContext
{
    public required string Subject { get; init; }
    public required string ClientId { get; init; }
    public required string Issuer { get; init; }
    public required int LifetimeSeconds { get; init; }
    public required string[] Scopes { get; init; }
    public string? Nonce { get; init; }
    public DateTimeOffset? AuthTime { get; init; }
    public Dictionary<string, string> AdditionalClaims { get; init; } = [];
}
