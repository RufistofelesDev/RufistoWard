namespace RufistoWard.Core.Tests.Endpoints;

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RufistoWard.Tokens.Persistence;

[Trait("Category", "Integration")]
public sealed class DiscoveryEndpointTests
{
    private static readonly Dictionary<string, string?> TestConfig = new()
    {
        ["Server:BehindReverseProxy"] = "false",
        ["Database:ConnectionString"] = "unused",
        ["Token:Issuer"] = "https://localhost:5001",
        ["Token:AccessTokenLifetimeSeconds"] = "3600",
        ["Token:RefreshTokenLifetimeSeconds"] = "86400",
        ["AdminAuth:Mode"] = "Local",
        ["KeyManagement:SigningKeyType"] = "RSA",
        ["SecurityHeaders:EnableHsts"] = "true",
        ["SecurityHeaders:EnableCsp"] = "true",
        ["RateLimits:TokenEndpointPermitLimit"] = "100",
        ["RateLimits:TokenEndpointWindowSeconds"] = "60",
    };

    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.Sources.Clear();
                    config.AddInMemoryCollection(TestConfig);
                });
                builder.ConfigureServices(services =>
                {
                    var efDescriptors = services
                        .Where(d => d.ServiceType.FullName?.Contains("EntityFrameworkCore") == true
                            || d.ServiceType == typeof(DbContextOptions<RufistoWardDbContext>)
                            || d.ServiceType == typeof(DbContextOptions)
                            || d.ServiceType == typeof(RufistoWardDbContext))
                        .ToList();
                    foreach (var d in efDescriptors)
                    {
                        services.Remove(d);
                    }

                    services.AddDbContext<RufistoWardDbContext>(options =>
                        options.UseInMemoryDatabase($"DiscoveryTests-{Guid.NewGuid()}"));
                });
            });
    }

    [Fact]
    public async Task DiscoveryEndpoint_ReturnsOkWithValidJson()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/.well-known/openid-configuration", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        Assert.Equal("https://localhost:5001", root.GetProperty("issuer").GetString());
        Assert.Equal("https://localhost:5001/connect/authorize", root.GetProperty("authorization_endpoint").GetString());
        Assert.Equal("https://localhost:5001/connect/token", root.GetProperty("token_endpoint").GetString());
        Assert.Equal("https://localhost:5001/.well-known/jwks", root.GetProperty("jwks_uri").GetString());
    }

    [Fact]
    public async Task DiscoveryEndpoint_ContainsAllRequiredOidcFields()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var content = await (await client.GetAsync("/.well-known/openid-configuration", ct))
            .Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        // All required OIDC Discovery 1.0 fields
        string[] requiredFields =
        [
            "issuer", "authorization_endpoint", "token_endpoint", "jwks_uri",
            "response_types_supported", "grant_types_supported", "subject_types_supported",
            "id_token_signing_alg_values_supported", "scopes_supported",
            "token_endpoint_auth_methods_supported", "claims_supported",
            "code_challenge_methods_supported",
        ];

        foreach (var field in requiredFields)
        {
            Assert.True(root.TryGetProperty(field, out _), $"Missing required field: {field}");
        }
    }

    [Fact]
    public async Task DiscoveryEndpoint_HasCacheControlAndETagHeaders()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/.well-known/openid-configuration", ct);

        Assert.True(response.Headers.Contains("ETag"), "Missing ETag header");
        Assert.Contains("public", response.Headers.CacheControl?.ToString() ?? "");
    }

    [Fact]
    public async Task JwksEndpoint_ReturnsOkWithEmptyKeysWhenNoSigningKey()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/.well-known/jwks", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(content);
        var keys = doc.RootElement.GetProperty("keys");
        Assert.Equal(0, keys.GetArrayLength());
    }

    [Fact]
    public async Task JwksEndpoint_HasCacheControlAndETagHeaders()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/.well-known/jwks", ct);

        Assert.True(response.Headers.Contains("ETag"), "Missing ETag header");
        Assert.Contains("public", response.Headers.CacheControl?.ToString() ?? "");
    }
}
