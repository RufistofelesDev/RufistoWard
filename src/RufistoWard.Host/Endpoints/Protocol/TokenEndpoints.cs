namespace RufistoWard.Core.Endpoints.Protocol;

using System.Text.Json;
using RufistoWard.Auth.Protocol.Common;
using RufistoWard.Auth.Protocol.Token;

public static class TokenEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static IEndpointRouteBuilder MapTokenEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/connect/token", async (HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var form = await httpContext.Request.ReadFormAsync(cancellationToken);

            var request = new TokenRequest
            {
                GrantType = form["grant_type"].ToString(),
                Code = form["code"].ToString(),
                RedirectUri = form["redirect_uri"].ToString(),
                ClientId = ExtractClientId(httpContext, form),
                ClientSecret = ExtractClientSecret(httpContext, form),
                CodeVerifier = form["code_verifier"].ToString(),
                RefreshToken = form["refresh_token"].ToString(),
                Scope = form["scope"].ToString(),
                Nonce = form["nonce"].ToString(),
            };

            if (string.IsNullOrWhiteSpace(request.GrantType))
            {
                return WriteError(ProtocolError.InvalidRequest("grant_type is required."));
            }

            // Resolve handler by grant_type
            var handlers = httpContext.RequestServices.GetServices<ITokenRequestHandler>();
            var handler = handlers.FirstOrDefault(h =>
                string.Equals(h.GrantType, request.GrantType, StringComparison.Ordinal));

            if (handler is null)
            {
                return WriteError(ProtocolError.UnsupportedGrantType($"Grant type '{request.GrantType}' is not supported."));
            }

            var result = await handler.HandleAsync(request, cancellationToken);

            if (result.IsSuccess)
            {
                return Results.Json(result.Value, JsonOptions, statusCode: 200);
            }

            return WriteError(result.Error!);
        });

        return endpoints;
    }

    private static IResult WriteError(ProtocolError error)
    {
        var body = new { error = error.Code, error_description = error.Description };
        return Results.Json(body, statusCode: error.HttpStatus);
    }

    private static string? ExtractClientId(HttpContext context, IFormCollection form)
    {
        // Try client_secret_basic (Authorization: Basic base64(id:secret))
        var authHeader = context.Request.Headers.Authorization.ToString();
        if (authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            var decoded = System.Text.Encoding.UTF8.GetString(
                Convert.FromBase64String(authHeader["Basic ".Length..]));
            var parts = decoded.Split(':', 2);
            if (parts.Length == 2)
            {
                return Uri.UnescapeDataString(parts[0]);
            }
        }

        // Fall back to client_secret_post
        return form["client_id"].ToString();
    }

    private static string? ExtractClientSecret(HttpContext context, IFormCollection form)
    {
        var authHeader = context.Request.Headers.Authorization.ToString();
        if (authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            var decoded = System.Text.Encoding.UTF8.GetString(
                Convert.FromBase64String(authHeader["Basic ".Length..]));
            var parts = decoded.Split(':', 2);
            if (parts.Length == 2)
            {
                return Uri.UnescapeDataString(parts[1]);
            }
        }

        return form["client_secret"].ToString();
    }
}
