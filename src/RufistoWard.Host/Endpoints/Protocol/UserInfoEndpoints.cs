namespace RufistoWard.Core.Endpoints.Protocol;

using RufistoWard.Auth.Protocol.UserInfo;

public static class UserInfoEndpoints
{
    public static IEndpointRouteBuilder MapUserInfoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/connect/userinfo", HandleUserInfoAsync);
        endpoints.MapPost("/connect/userinfo", HandleUserInfoAsync);
        return endpoints;
    }

    private static async Task<IResult> HandleUserInfoAsync(
        HttpContext httpContext,
        IUserInfoHandler handler,
        CancellationToken cancellationToken)
    {
        var authHeader = httpContext.Request.Headers.Authorization.ToString();

        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            httpContext.Response.Headers["WWW-Authenticate"] = "Bearer";
            return Results.Json(
                new { error = "invalid_request", error_description = "A Bearer access token is required." },
                statusCode: 401);
        }

        var token = authHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(token))
        {
            httpContext.Response.Headers["WWW-Authenticate"] = "Bearer";
            return Results.Json(
                new { error = "invalid_request", error_description = "A Bearer access token is required." },
                statusCode: 401);
        }

        var result = await handler.HandleAsync(token, cancellationToken);

        if (result.IsSuccess)
        {
            return Results.Json(result.Value);
        }

        var error = result.Error!;
        var wwwAuth = error.HttpStatus switch
        {
            403 => $"Bearer error=\"{error.Code}\", scope=\"openid\"",
            _ => $"Bearer error=\"{error.Code}\", error_description=\"{error.Description}\"",
        };
        httpContext.Response.Headers["WWW-Authenticate"] = wwwAuth;

        return Results.Json(
            new { error = error.Code, error_description = error.Description },
            statusCode: error.HttpStatus);
    }
}
