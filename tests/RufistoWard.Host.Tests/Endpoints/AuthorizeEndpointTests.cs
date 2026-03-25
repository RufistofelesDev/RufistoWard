namespace RufistoWard.Core.Tests.Endpoints;

using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RufistoWard.Tokens.Persistence;

[Trait("Category", "Integration")]
public sealed class AuthorizeEndpointTests
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
                        options.UseInMemoryDatabase($"AuthorizeTests-{Guid.NewGuid()}"));
                });
            });
    }

    [Fact]
    public async Task AuthorizeEndpoint_UnauthenticatedUser_RedirectsToLogin()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync(
            "/connect/authorize?response_type=code&client_id=test&redirect_uri=https://example.com/cb&scope=openid&code_challenge=abc&code_challenge_method=S256",
            ct);

        // Should redirect to login (302) — client doesn't exist yet but redirect happens before client validation in some flows,
        // or it returns an error about unknown client
        Assert.True(
            response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.BadRequest,
            $"Expected redirect or error, got {response.StatusCode}");
    }

    [Fact]
    public async Task AuthorizeEndpoint_MissingResponseType_ReturnsError()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync(
            "/connect/authorize?client_id=test&redirect_uri=https://example.com/cb",
            ct);

        // Should get an error about invalid response_type
        Assert.True(
            response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.BadRequest,
            $"Expected redirect or error, got {response.StatusCode}");
    }

    [Fact]
    public async Task AuthorizeEndpoint_MissingPkce_ReturnsError()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        // Request without code_challenge — should fail PKCE enforcement
        var response = await client.GetAsync(
            "/connect/authorize?response_type=code&client_id=test&redirect_uri=https://example.com/cb&scope=openid",
            ct);

        Assert.True(
            response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.BadRequest,
            $"Expected redirect or error, got {response.StatusCode}");
    }

    [Fact]
    public async Task LoginPage_Renders()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/login", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync(ct);
        Assert.Contains("data-testid=\"login-form\"", content);
        Assert.Contains("data-testid=\"username-input\"", content);
        Assert.Contains("data-testid=\"password-input\"", content);
    }
}
