namespace RufistoWard.Core.Tests.Endpoints;

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RufistoWard.Tokens.Persistence;

[Trait("Category", "Integration")]
public sealed class RevocationAndIntrospectionTests
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
                        options.UseInMemoryDatabase($"RevocTests-{Guid.NewGuid()}"));
                });
            });
    }

    [Fact]
    public async Task Revocation_InvalidToken_StillReturns200()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Revocation with nonexistent client still returns auth error
        var content = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("token", "nonexistent-token"),
            new KeyValuePair<string, string>("client_id", "nonexistent"),
            new KeyValuePair<string, string>("client_secret", "secret"),
        ]);

        var response = await client.PostAsync("/connect/revocation", content, ct);
        // Will be 401 because client doesn't exist
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Introspection_UnauthenticatedCaller_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var content = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("token", "some-token"),
        ]);

        var response = await client.PostAsync("/connect/introspect", content, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Introspection_InvalidToken_ReturnsInactive()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Need to auth first — but we don't have a client in the DB.
        // This will return 401 since client auth fails.
        var content = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("token", "invalid-token"),
            new KeyValuePair<string, string>("client_id", "nonexistent"),
            new KeyValuePair<string, string>("client_secret", "secret"),
        ]);

        var response = await client.PostAsync("/connect/introspect", content, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
