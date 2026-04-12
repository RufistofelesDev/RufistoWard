namespace RufistoWard.Core.Endpoints.Protocol;

using System.Text;
using System.Text.Json;
using RufistoWard.Auth.Protocol.UserInfo;
using RufistoWard.Auth.Protocol.Validation;
using RufistoWard.Identity.Interfaces;

public static class IntrospectionEndpoints
{
    public static IEndpointRouteBuilder MapIntrospectionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/connect/introspect", async (
            HttpContext httpContext,
            IClientAuthenticator clientAuthenticator,
            IPersistedGrantRepository grantRepository,
            IAccessTokenValidator accessTokenValidator,
            CancellationToken cancellationToken) =>
        {
            var form = await httpContext.Request.ReadFormAsync(cancellationToken);
            var token = form["token"].ToString();

            // Extract client credentials from form or Basic auth
            var clientId = form["client_id"].ToString();
            var clientSecret = form["client_secret"].ToString();

            var authHeader = httpContext.Request.Headers.Authorization.ToString();
            if (authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            {
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(authHeader["Basic ".Length..]));
                var parts = decoded.Split(':', 2);
                if (parts.Length == 2)
                {
                    clientId = Uri.UnescapeDataString(parts[0]);
                    clientSecret = Uri.UnescapeDataString(parts[1]);
                }
            }

            // Authenticate caller (resource server)
            var clientResult = await clientAuthenticator.AuthenticateAsync(clientId, clientSecret, cancellationToken);
            if (!clientResult.IsSuccess)
            {
                return Results.Json(
                    new { error = "invalid_client", error_description = "Client authentication failed." },
                    statusCode: 401);
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                return Results.Json(new { active = false });
            }

            // Check if it's a reference token / persisted grant
            var grant = await grantRepository.FindByIdAsync(token, cancellationToken);
            if (grant is not null && !grant.IsConsumed && !grant.IsExpired)
            {
                return Results.Json(new
                {
                    active = true,
                    scope = TryGetScope(grant.Data),
                    client_id = grant.ClientId,
                    sub = grant.SubjectId,
                    token_type = grant.Type == "refresh_token" ? "refresh_token" : "access_token",
                });
            }

            // Validate JWT with full signature verification
            var validationResult = await accessTokenValidator.ValidateAsync(token, cancellationToken);
            if (validationResult.IsSuccess)
            {
                var validated = validationResult.Value!;
                return Results.Json(new
                {
                    active = true,
                    scope = string.Join(' ', validated.Scopes),
                    client_id = validated.ClientId,
                    sub = validated.Subject,
                    token_type = "access_token",
                });
            }

            return Results.Json(new { active = false });
        });

        return endpoints;
    }

    private static string? TryGetScope(string data)
    {
        try
        {
            using var doc = JsonDocument.Parse(data);
            return doc.RootElement.TryGetProperty("scope", out var scope) ? scope.GetString() : null;
        }
        catch
        {
            return null;
        }
    }
}
