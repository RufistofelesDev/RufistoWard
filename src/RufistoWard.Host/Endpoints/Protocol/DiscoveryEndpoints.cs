namespace RufistoWard.Core.Endpoints.Protocol;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RufistoWard.Auth.Protocol.Discovery;

public static class DiscoveryEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static IEndpointRouteBuilder MapDiscoveryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/.well-known/openid-configuration", async (
            IDiscoveryService discoveryService,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var doc = await discoveryService.GetDiscoveryDocumentAsync(cancellationToken);
            var json = JsonSerializer.Serialize(doc, JsonOptions);

            SetCacheHeaders(httpContext, json);
            return Results.Content(json, "application/json");
        });

        endpoints.MapGet("/.well-known/jwks", async (
            IJwksService jwksService,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var doc = await jwksService.GetJwksDocumentAsync(cancellationToken);
            var json = JsonSerializer.Serialize(doc, JsonOptions);

            SetCacheHeaders(httpContext, json);
            return Results.Content(json, "application/json");
        });

        return endpoints;
    }

    private static void SetCacheHeaders(HttpContext httpContext, string content)
    {
        httpContext.Response.Headers.CacheControl = "public, max-age=3600";
        httpContext.Response.Headers.ETag = $"\"{ComputeETag(content)}\"";
    }

    private static string ComputeETag(string content)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexStringLower(hash[..8]);
    }
}
