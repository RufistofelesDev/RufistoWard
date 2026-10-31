namespace RufistoWard.Core.Endpoints.Protocol;

using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Web;
using Microsoft.AspNetCore.WebUtilities;
using RufistoWard.Auth.Protocol.Authorization;
using RufistoWard.Identity.Interfaces;

public static class AuthorizeEndpoints
{
    public static IEndpointRouteBuilder MapAuthorizeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // OIDCC-3.1.2.1 requires the authorization endpoint to accept BOTH GET and POST.
        endpoints.MapGet("/connect/authorize", (
            HttpContext httpContext,
            AuthorizationRequestHandler requestHandler,
            AuthorizationCodeGenerator codeGenerator,
            IClientRepository clientRepository,
            CancellationToken cancellationToken) =>
            HandleAsync(
                httpContext,
                key => httpContext.Request.Query[key],
                requestHandler,
                codeGenerator,
                clientRepository,
                cancellationToken));

        endpoints.MapPost("/connect/authorize", async (
            HttpContext httpContext,
            AuthorizationRequestHandler requestHandler,
            AuthorizationCodeGenerator codeGenerator,
            IClientRepository clientRepository,
            CancellationToken cancellationToken) =>
        {
            if (!httpContext.Request.HasFormContentType)
            {
                return Results.BadRequest(new
                {
                    error = "invalid_request",
                    error_description = "POST to the authorization endpoint must use application/x-www-form-urlencoded.",
                });
            }

            var form = await httpContext.Request.ReadFormAsync(cancellationToken);
            return await HandleAsync(
                httpContext,
                key => form[key],
                requestHandler,
                codeGenerator,
                clientRepository,
                cancellationToken);
        });

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext httpContext,
        Func<string, string?> param,
        AuthorizationRequestHandler requestHandler,
        AuthorizationCodeGenerator codeGenerator,
        IClientRepository clientRepository,
        CancellationToken cancellationToken)
    {
        var request = new AuthorizeRequest
        {
            ResponseType = param("response_type"),
            ClientId = param("client_id"),
            RedirectUri = param("redirect_uri"),
            Scope = param("scope"),
            State = param("state"),
            CodeChallenge = param("code_challenge"),
            CodeChallengeMethod = param("code_challenge_method"),
            Nonce = param("nonce"),
            Request = param("request"),
            RequestUri = param("request_uri"),
            ResponseMode = param("response_mode"),
        };

        var prompts = (param("prompt") ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var promptNone = prompts.Contains("none", StringComparer.Ordinal);
        var promptLogin = prompts.Contains("login", StringComparer.Ordinal);

        // Validate request
        var validationResult = await requestHandler.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsSuccess)
        {
            var error = validationResult.Error!;

            // Only redirect if the redirect_uri was independently verified as registered
            var canRedirect = false;
            if (!string.IsNullOrEmpty(request.RedirectUri) && !string.IsNullOrEmpty(request.ClientId)
                && error.Code != "invalid_redirect_uri")
            {
                var client = await clientRepository.FindByClientIdAsync(request.ClientId, cancellationToken);
                canRedirect = client?.RedirectUris.Any(r => r.Uri == request.RedirectUri) == true;
            }

            if (canRedirect)
            {
                // Validation failed, so response_mode was never validated: honour form_post only when it
                // was asked for explicitly, and fall back to query for anything else, including an
                // unsupported mode — that is the very error being reported.
                var errorMode = string.Equals(request.ResponseMode, ResponseModes.FormPost, StringComparison.Ordinal)
                    ? ResponseModes.FormPost
                    : ResponseModes.Query;
                return ErrorResponse(httpContext, request.RedirectUri!, errorMode, error.Code, error.Description, request.State);
            }

            return Results.BadRequest(new { error = error.Code, error_description = error.Description });
        }

        var validated = validationResult.Value!;

        // OIDCC-3.1.2.1: "none" must not be combined with any other prompt value.
        if (promptNone && prompts.Length > 1)
        {
            return ErrorResponse(
                httpContext,
                validated.RedirectUri,
                validated.ResponseMode,
                "invalid_request",
                "prompt=none must not be combined with other prompt values.",
                validated.State);
        }

        var user = httpContext.User;
        var isAuthenticated = user.Identity?.IsAuthenticated == true;
        var authTime = isAuthenticated ? ReadAuthTime(user) : null;

        // max_age (OIDCC-3.1.2.1): re-authenticate when the existing authentication is too old.
        // A session with no recorded auth_time cannot satisfy max_age, so treat it as stale.
        var maxAgeExceeded = false;
        if (long.TryParse(param("max_age"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxAge) && maxAge >= 0)
        {
            maxAgeExceeded = authTime is null
                || DateTimeOffset.UtcNow - authTime.Value > TimeSpan.FromSeconds(maxAge);
        }

        var needsAuthentication = !isAuthenticated || promptLogin || maxAgeExceeded;

        if (needsAuthentication)
        {
            // OIDCC-3.1.2.6: prompt=none forbids any UI, so an unusable session is an error, not a login page.
            if (promptNone)
            {
                return ErrorResponse(
                    httpContext,
                    validated.RedirectUri,
                    validated.ResponseMode,
                    "login_required",
                    "Authentication is required but prompt=none was requested.",
                    validated.State);
            }

            // Re-enter authorize as a GET once the user has authenticated. prompt=login is dropped from
            // the return URL — it has been honoured, and leaving it in would loop forever.
            var returnUrl = "/connect/authorize" + BuildQuery(param, dropPromptLogin: true);
            return Results.Redirect($"/login?returnUrl={HttpUtility.UrlEncode(returnUrl)}");
        }

        // User is authenticated — issue authorization code
        var subjectId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub") ?? "";

        var authorizationCode = await codeGenerator.GenerateAsync(subjectId, validated, authTime, cancellationToken);

        return Respond(httpContext, validated.RedirectUri, validated.ResponseMode,
        [
            new("code", authorizationCode),
            new("state", validated.State),
        ]);
    }

    private static readonly string[] ForwardedParameters =
    [
        "response_type", "response_mode", "client_id", "redirect_uri", "scope", "state",
        "code_challenge", "code_challenge_method", "nonce", "max_age",
        "login_hint", "ui_locales", "claims_locales", "acr_values", "id_token_hint", "display",
    ];

    /// <summary>
    /// Rebuilds the authorization request as a query string so a POST request can resume as a GET
    /// after interactive login.
    /// </summary>
    private static string BuildQuery(Func<string, string?> param, bool dropPromptLogin)
    {
        var parts = new List<string>();

        foreach (var name in ForwardedParameters)
        {
            var value = param(name);
            if (!string.IsNullOrEmpty(value))
            {
                parts.Add($"{name}={HttpUtility.UrlEncode(value)}");
            }
        }

        var remainingPrompts = (param("prompt") ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => !dropPromptLogin || !string.Equals(p, "login", StringComparison.Ordinal))
            .ToArray();

        if (remainingPrompts.Length > 0)
        {
            parts.Add($"prompt={HttpUtility.UrlEncode(string.Join(' ', remainingPrompts))}");
        }

        return parts.Count > 0 ? "?" + string.Join('&', parts) : string.Empty;
    }

    private static DateTimeOffset? ReadAuthTime(ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue("auth_time");
        return long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;
    }

    private static IResult ErrorResponse(
        HttpContext httpContext, string redirectUri, string responseMode, string code, string? description, string? state)
        => Respond(httpContext, redirectUri, responseMode,
        [
            new("error", code),
            new("error_description", description),
            new("state", state),
        ]);

    /// <summary>
    /// Delivers an authorization response to an already-validated redirect_uri in the requested
    /// response mode. Empty parameters are omitted.
    /// </summary>
    private static IResult Respond(
        HttpContext httpContext, string redirectUri, string responseMode, KeyValuePair<string, string?>[] parameters)
    {
        var present = parameters.Where(p => !string.IsNullOrEmpty(p.Value)).ToArray();

        // The scheme check repeats validation on purpose: this is the sink. Error paths reach here
        // with a response mode that was never validated, and a javascript: action would run script
        // on this origin. A Location header with such a scheme is not executed by browsers.
        if (!string.Equals(responseMode, ResponseModes.FormPost, StringComparison.Ordinal)
            || !ResponseModes.CanFormPostTo(redirectUri))
        {
            // AddQueryString appends with '&' when the registered redirect_uri already has a query.
            return Results.Redirect(QueryHelpers.AddQueryString(redirectUri, present));
        }

        // Form Post Response Mode: an HTML page that POSTs the parameters to the redirect_uri, so the
        // code never appears in a URL, browser history or a Referer header. The inline onload is
        // covered by the CSP's script-src 'unsafe-inline'; <noscript> keeps it usable without script.
        var html = new StringBuilder()
            .Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>Continue</title></head>")
            .Append("<body onload=\"document.forms[0].submit()\">")
            .Append(CultureInfo.InvariantCulture, $"<form method=\"post\" action=\"{HtmlEncoder.Default.Encode(redirectUri)}\">");

        foreach (var (name, value) in present)
        {
            html.Append(CultureInfo.InvariantCulture,
                $"<input type=\"hidden\" name=\"{HtmlEncoder.Default.Encode(name)}\" value=\"{HtmlEncoder.Default.Encode(value!)}\">");
        }

        html.Append("<noscript><button type=\"submit\">Continue</button></noscript></form></body></html>");

        httpContext.Response.Headers.CacheControl = "no-store";
        httpContext.Response.Headers.Pragma = "no-cache";
        return Results.Content(html.ToString(), "text/html; charset=utf-8");
    }
}
