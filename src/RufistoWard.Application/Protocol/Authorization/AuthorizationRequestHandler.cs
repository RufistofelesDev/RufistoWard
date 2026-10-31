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
    public string? Request { get; init; }
    public string? RequestUri { get; init; }
    public string? ResponseMode { get; init; }
}

public static class ResponseModes
{
    public const string Query = "query";
    public const string FormPost = "form_post";

    /// <summary>What discovery advertises as <c>response_modes_supported</c>.</summary>
    public static readonly string[] Supported = [Query, FormPost];

    /// <summary>
    /// form_post renders the redirect_uri as a form action that submits itself, so anything but
    /// http(s) — javascript:, data:, a native app's custom scheme — must never reach it.
    /// </summary>
    public static bool CanFormPostTo(string redirectUri)
        => Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}

public sealed record ValidatedAuthorizeRequest
{
    public required string ResponseMode { get; init; }
    public required string ClientId { get; init; }
    public required string RedirectUri { get; init; }
    public required string Scope { get; init; }
    public required string? State { get; init; }
    public required string? CodeChallenge { get; init; }
    public required string? CodeChallengeMethod { get; init; }
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

        // response_mode (OAuth 2.0 Multiple Response Types, Form Post Response Mode). Absent means
        // query for response_type=code. Anything not advertised is rejected rather than ignored:
        // a client that asked for form_post and silently got query is relying on its own leniency.
        var responseMode = string.IsNullOrEmpty(request.ResponseMode) ? ResponseModes.Query : request.ResponseMode;
        if (!ResponseModes.Supported.Contains(responseMode, StringComparer.Ordinal))
        {
            return Result<ValidatedAuthorizeRequest>.Failure(
                ProtocolError.InvalidRequest($"response_mode '{responseMode}' is not supported."));
        }

        if (responseMode == ResponseModes.FormPost && !ResponseModes.CanFormPostTo(request.RedirectUri))
        {
            return Result<ValidatedAuthorizeRequest>.Failure(
                ProtocolError.InvalidRequest("response_mode=form_post requires an http or https redirect_uri."));
        }

        // Request objects (OIDCC-6.1/6.2). Discovery advertises neither `request_parameter_supported`
        // nor `request_uri_parameter_supported`, so these MUST be rejected rather than ignored —
        // silently dropping them loses the state/nonce carried inside the object.
        // Checked after redirect_uri validation so the error can be returned to a registered URI.
        if (!string.IsNullOrWhiteSpace(request.RequestUri))
        {
            return Result<ValidatedAuthorizeRequest>.Failure(
                new ProtocolError("request_uri_not_supported", "The request_uri parameter is not supported."));
        }

        if (!string.IsNullOrWhiteSpace(request.Request))
        {
            return Result<ValidatedAuthorizeRequest>.Failure(
                new ProtocolError("request_not_supported", "The request parameter is not supported."));
        }

        // PKCE (FR12): enforced per client policy (RequirePkce, default true). The OIDF Basic OP
        // profile never sends code_challenge, so PKCE-optional clients must be admitted without it;
        // voluntary PKCE is still validated and carried through to the token exchange.
        var hasCodeChallenge = !string.IsNullOrWhiteSpace(request.CodeChallenge);
        if (client.RequirePkce && !hasCodeChallenge)
        {
            return Result<ValidatedAuthorizeRequest>.Failure(
                ProtocolError.InvalidRequest("code_challenge is required (PKCE enforcement)."));
        }

        if (hasCodeChallenge && !string.Equals(request.CodeChallengeMethod, "S256", StringComparison.Ordinal))
        {
            return Result<ValidatedAuthorizeRequest>.Failure(
                ProtocolError.InvalidRequest("Only code_challenge_method=S256 is supported."));
        }

        return Result<ValidatedAuthorizeRequest>.Success(new ValidatedAuthorizeRequest
        {
            ResponseMode = responseMode,
            ClientId = request.ClientId,
            RedirectUri = request.RedirectUri,
            Scope = request.Scope ?? "openid",
            State = request.State,
            CodeChallenge = hasCodeChallenge ? request.CodeChallenge : null,
            CodeChallengeMethod = hasCodeChallenge ? request.CodeChallengeMethod : null,
            Nonce = request.Nonce,
        });
    }
}
