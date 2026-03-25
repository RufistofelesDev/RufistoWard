namespace RufistoWard.Auth.Protocol.Token;

using RufistoWard.Auth.Protocol.Common;

public interface ITokenRequestHandler
{
    string GrantType { get; }
    Task<Result<TokenResponse>> HandleAsync(TokenRequest request, CancellationToken cancellationToken = default);
}

public sealed record TokenRequest
{
    public required string GrantType { get; init; }
    public string? Code { get; init; }
    public string? RedirectUri { get; init; }
    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }
    public string? CodeVerifier { get; init; }
    public string? RefreshToken { get; init; }
    public string? Scope { get; init; }
    public string? Nonce { get; init; }
}
