namespace RufistoWard.Core.Endpoints.Protocol;

using System.Web;
using Microsoft.AspNetCore.Authentication;

public static class EndSessionEndpoints
{
    public static IEndpointRouteBuilder MapEndSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/connect/endsession", async (
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var query = httpContext.Request.Query;
            var postLogoutRedirectUri = query["post_logout_redirect_uri"].ToString();

            // Sign out the user
            if (httpContext.User.Identity?.IsAuthenticated == true)
            {
                await httpContext.SignOutAsync("IdentityServerCookie");
            }

            // Redirect to post_logout_redirect_uri if provided
            if (!string.IsNullOrWhiteSpace(postLogoutRedirectUri))
            {
                var state = query["state"].ToString();
                var redirectUrl = postLogoutRedirectUri;
                if (!string.IsNullOrEmpty(state))
                {
                    redirectUrl += $"?state={HttpUtility.UrlEncode(state)}";
                }
                return Results.Redirect(redirectUrl);
            }

            return Results.Ok(new { message = "Logged out successfully." });
        });

        return endpoints;
    }
}
