namespace RufistoWard.Core.Tests.Endpoints;

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RufistoWard.Tokens.Persistence;

[Trait("Category", "Integration")]
public sealed class TokenEndpointTests
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
        ["KeyManagement:SigningAlgorithm"] = "RS256",
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
                        options.UseInMemoryDatabase($"TokenTests-{Guid.NewGuid()}"));
                });
            });
    }

    [Fact]
    public async Task TokenEndpoint_MissingGrantType_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var content = new FormUrlEncodedContent([]);
        var response = await client.PostAsync("/connect/token", content, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("invalid_request", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task TokenEndpoint_UnsupportedGrantType_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var content = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("grant_type", "password"),
        ]);
        var response = await client.PostAsync("/connect/token", content, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("unsupported_grant_type", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task TokenEndpoint_AuthCodeWithoutCode_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var content = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
            new KeyValuePair<string, string>("client_id", "test"),
            new KeyValuePair<string, string>("redirect_uri", "https://example.com/callback"),
        ]);
        var response = await client.PostAsync("/connect/token", content, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        var error = doc.RootElement.GetProperty("error").GetString();
        Assert.True(error is "invalid_request" or "invalid_client",
            $"Expected invalid_request or invalid_client, got {error}");
    }

    [Fact]
    public async Task TokenEndpoint_RefreshTokenWithoutToken_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var content = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("grant_type", "refresh_token"),
            new KeyValuePair<string, string>("client_id", "test"),
        ]);
        var response = await client.PostAsync("/connect/token", content, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        var error = doc.RootElement.GetProperty("error").GetString();
        Assert.True(error is "invalid_request" or "invalid_client",
            $"Expected invalid_request or invalid_client, got {error}");
    }

    [Fact]
    public async Task TokenEndpoint_RefreshTokenInvalid_ReturnsInvalidGrant()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var content = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("grant_type", "refresh_token"),
            new KeyValuePair<string, string>("refresh_token", "nonexistent-token"),
            new KeyValuePair<string, string>("client_id", "test"),
        ]);
        var response = await client.PostAsync("/connect/token", content, ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        var error = doc.RootElement.GetProperty("error").GetString();
        Assert.True(error is "invalid_grant" or "invalid_client",
            $"Expected invalid_grant or invalid_client, got {error}");
    }

    [Fact]
    public async Task TokenEndpoint_ClientCredentialsWithoutClient_ReturnsError()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var content = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("client_id", "nonexistent"),
            new KeyValuePair<string, string>("client_secret", "secret"),
            new KeyValuePair<string, string>("scope", "api"),
        ]);
        var response = await client.PostAsync("/connect/token", content, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("invalid_client", doc.RootElement.GetProperty("error").GetString());
    }
}
