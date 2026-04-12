namespace RufistoWard.Core.Endpoints.Protocol;

using System.Text;
using RufistoWard.Auth.Protocol.Validation;
using RufistoWard.Identity.Interfaces;

public static class RevocationEndpoints
{
    public static IEndpointRouteBuilder MapRevocationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/connect/revocation", async (
            HttpContext httpContext,
            IClientAuthenticator clientAuthenticator,
            IPersistedGrantRepository grantRepository,
            CancellationToken cancellationToken) =>
        {
            var form = await httpContext.Request.ReadFormAsync(cancellationToken);
            var token = form["token"].ToString();
            var clientId = form["client_id"].ToString();
            var clientSecret = form["client_secret"].ToString();

            // Support Basic auth header (RFC 7009)
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

            // Authenticate client
            var clientResult = await clientAuthenticator.AuthenticateAsync(clientId, clientSecret, cancellationToken);
            if (!clientResult.IsSuccess)
            {
                return Results.Json(
                    new { error = clientResult.Error!.Code, error_description = clientResult.Error.Description },
                    statusCode: 401);
            }

            if (!string.IsNullOrWhiteSpace(token))
            {
                // Try to find and revoke the token
                var grant = await grantRepository.FindByIdAsync(token, cancellationToken);
                if (grant is not null && grant.ClientId == clientResult.Value!.ClientId)
                {
                    grant.MarkConsumed();
                    await grantRepository.UpdateAsync(grant, cancellationToken);

                    // If refresh token, also revoke associated grants for this subject+client
                    if (grant.Type == "refresh_token" && grant.SubjectId is not null)
                    {
                        var related = await grantRepository.FindBySubjectIdAsync(grant.SubjectId, cancellationToken);
                        foreach (var r in related.Where(r => r.ClientId == grant.ClientId && !r.IsConsumed && r.Id != grant.Id))
                        {
                            r.MarkConsumed();
                            await grantRepository.UpdateAsync(r, cancellationToken);
                        }
                    }
                }
            }

            // RFC 7009: always return 200, never reveal token status
            return Results.Ok();
        });

        return endpoints;
    }
}
