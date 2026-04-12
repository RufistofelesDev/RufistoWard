namespace RufistoWard.Core.Endpoints.Protocol;

using System.Text;
using System.Text.Json;
using System.Web;
using Microsoft.AspNetCore.Authentication;
using RufistoWard.Identity.Interfaces;

public static class EndSessionEndpoints
{
    public static IEndpointRouteBuilder MapEndSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/connect/endsession", async (
            HttpContext httpContext,
            IClientRepository clientRepository,
            CancellationToken cancellationToken) =>
        {
            var query = httpContext.Request.Query;
            var postLogoutRedirectUri = query["post_logout_redirect_uri"].ToString();

            // Sign out the user
            if (httpContext.User.Identity?.IsAuthenticated == true)
            {
                await httpContext.SignOutAsync("IdentityServerCookie");
            }

            // Redirect to post_logout_redirect_uri only if validated against registered URIs
            if (!string.IsNullOrWhiteSpace(postLogoutRedirectUri))
            {
                var idTokenHint = query["id_token_hint"].ToString();
                var isValidRedirect = false;

                if (!string.IsNullOrWhiteSpace(idTokenHint))
                {
                    var clientId = TryExtractAudience(idTokenHint);
                    if (clientId is not null)
                    {
                        var client = await clientRepository.FindByClientIdAsync(clientId, cancellationToken);
                        if (client is not null)
                        {
                            isValidRedirect = client.RedirectUris.Any(r =>
                                string.Equals(r.Uri, postLogoutRedirectUri, StringComparison.Ordinal));
                        }
                    }
                }

                if (isValidRedirect)
                {
                    var state = query["state"].ToString();
                    var redirectUrl = postLogoutRedirectUri;
                    if (!string.IsNullOrEmpty(state))
                    {
                        redirectUrl += $"?state={HttpUtility.UrlEncode(state)}";
                    }
                    return Results.Redirect(redirectUrl);
                }
            }

            return Results.Ok(new { message = "Logged out successfully." });
        });

        return endpoints;
    }

    private static string? TryExtractAudience(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length != 3) return null;

        try
        {
            var padded = parts[1].Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2: padded += "=="; break;
                case 3: padded += "="; break;
            }
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("aud", out var aud) ? aud.GetString() : null;
        }
        catch
        {
            return null;
        }
    }
}
