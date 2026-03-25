namespace RufistoWard.Core.Endpoints.Protocol;

using System.Security.Claims;
using System.Web;
using RufistoWard.Auth.Protocol.Authorization;

public static class AuthorizeEndpoints
{
    public static IEndpointRouteBuilder MapAuthorizeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/connect/authorize", async (
            HttpContext httpContext,
            AuthorizationRequestHandler requestHandler,
            AuthorizationCodeGenerator codeGenerator,
            CancellationToken cancellationToken) =>
        {
            var query = httpContext.Request.Query;

            var request = new AuthorizeRequest
            {
                ResponseType = query["response_type"],
                ClientId = query["client_id"],
                RedirectUri = query["redirect_uri"],
                Scope = query["scope"],
                State = query["state"],
                CodeChallenge = query["code_challenge"],
                CodeChallengeMethod = query["code_challenge_method"],
                Nonce = query["nonce"],
            };

            // Validate request
            var validationResult = await requestHandler.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsSuccess)
            {
                var error = validationResult.Error!;
                // For invalid_redirect_uri, show error page (never redirect)
                if (error.Code == "invalid_redirect_uri")
                {
                    return Results.BadRequest(new { error = error.Code, error_description = error.Description });
                }

                // For other errors with a valid redirect_uri, redirect with error
                if (!string.IsNullOrEmpty(request.RedirectUri))
                {
                    var errorRedirect = $"{request.RedirectUri}?error={error.Code}&error_description={HttpUtility.UrlEncode(error.Description)}";
                    if (!string.IsNullOrEmpty(request.State))
                    {
                        errorRedirect += $"&state={HttpUtility.UrlEncode(request.State)}";
                    }
                    return Results.Redirect(errorRedirect);
                }

                return Results.BadRequest(new { error = error.Code, error_description = error.Description });
            }

            var validated = validationResult.Value!;

            // Check if user is authenticated
            var user = httpContext.User;
            if (user.Identity?.IsAuthenticated != true)
            {
                // Store authorize params and redirect to login
                var returnUrl = httpContext.Request.Path + httpContext.Request.QueryString;
                return Results.Redirect($"/login?returnUrl={HttpUtility.UrlEncode(returnUrl)}");
            }

            // User is authenticated — issue authorization code
            var subjectId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub") ?? "";

            var code = await codeGenerator.GenerateAsync(subjectId, validated, cancellationToken);

            var redirectUrl = $"{validated.RedirectUri}?code={code}";
            if (!string.IsNullOrEmpty(validated.State))
            {
                redirectUrl += $"&state={HttpUtility.UrlEncode(validated.State)}";
            }

            return Results.Redirect(redirectUrl);
        });

        return endpoints;
    }
}
