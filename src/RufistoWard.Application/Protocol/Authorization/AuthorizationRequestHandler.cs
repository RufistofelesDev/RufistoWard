namespace RufistoWard.Auth.Protocol.Authorization;

using RufistoWard.Auth.Protocol.Common;
using RufistoWard.Identity.Interfaces;

public sealed record AuthorizeRequest
{
    public string? ResponseType { get; init; }
    public string? ClientId { get; init; }
    public string? RedirectUri { get; init; }
    public string? Scope { get; init; }
    public string? State { get; init; }
    public string? CodeChallenge { get; init; }
    public string? CodeChallengeMethod { get; init; }
    public string? Nonce { get; init; }
}

public sealed record ValidatedAuthorizeRequest
{
    public required string ClientId { get; init; }
    public required string RedirectUri { get; init; }
    public required string Scope { get; init; }
    public required string? State { get; init; }
    public required string CodeChallenge { get; init; }
    public required string CodeChallengeMethod { get; init; }
    public required string? Nonce { get; init; }
}

public sealed class AuthorizationRequestHandler(IClientRepository clientRepository)
{
    public async Task<Result<ValidatedAuthorizeRequest>> ValidateAsync(
        AuthorizeRequest request,
        CancellationToken cancellationToken = default)
    {
        // Validate response_type
        if (!string.Equals(request.ResponseType, "code", StringComparison.Ordinal))
        {
            return Result<ValidatedAuthorizeRequest>.Failure(
                ProtocolError.InvalidRequest("Only response_type=code is supported."));
        }

        // Validate client
        if (string.IsNullOrWhiteSpace(request.ClientId))
        {
            return Result<ValidatedAuthorizeRequest>.Failure(
                ProtocolError.InvalidRequest("client_id is required."));
        }

        var client = await clientRepository.FindByClientIdAsync(request.ClientId, cancellationToken);
        if (client is null || !client.Enabled)
        {
            return Result<ValidatedAuthorizeRequest>.Failure(
                ProtocolError.InvalidClient("Unknown or disabled client."));
        }

        // Validate redirect_uri
        if (string.IsNullOrWhiteSpace(request.RedirectUri))
        {
            return Result<ValidatedAuthorizeRequest>.Failure(
                ProtocolError.InvalidRequest("redirect_uri is required."));
        }

        var validRedirectUri = client.RedirectUris.Any(r => r.Uri == request.RedirectUri);
        if (!validRedirectUri)
        {
            // Never redirect to unregistered URIs — return error directly
            return Result<ValidatedAuthorizeRequest>.Failure(
                new ProtocolError("invalid_redirect_uri", "The redirect_uri is not registered for this client."));
        }

        // PKCE required (FR12)
        if (string.IsNullOrWhiteSpace(request.CodeChallenge))
        {
            return Result<ValidatedAuthorizeRequest>.Failure(
                ProtocolError.InvalidRequest("code_challenge is required (PKCE enforcement)."));
        }

        if (!string.Equals(request.CodeChallengeMethod, "S256", StringComparison.Ordinal))
        {
            return Result<ValidatedAuthorizeRequest>.Failure(
                ProtocolError.InvalidRequest("Only code_challenge_method=S256 is supported."));
        }

        return Result<ValidatedAuthorizeRequest>.Success(new ValidatedAuthorizeRequest
        {
            ClientId = request.ClientId,
            RedirectUri = request.RedirectUri,
            Scope = request.Scope ?? "openid",
            State = request.State,
            CodeChallenge = request.CodeChallenge!,
            CodeChallengeMethod = request.CodeChallengeMethod!,
            Nonce = request.Nonce,
        });
    }
}
