namespace RufistoWard.Core.Tests.Middleware;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RufistoWard.Tokens.Persistence;

[Trait("Category", "Integration")]
public sealed class SecurityHeadersTests
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
                        options.UseInMemoryDatabase($"SecurityTests-{Guid.NewGuid()}"));
                });
            });
    }

    [Fact]
    public async Task AllResponses_IncludeXContentTypeOptions()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/.well-known/openid-configuration", ct);

        Assert.True(response.Headers.Contains("X-Content-Type-Options"));
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").First());
    }

    [Fact]
    public async Task AllResponses_IncludeXFrameOptions()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/.well-known/openid-configuration", ct);

        Assert.True(response.Headers.Contains("X-Frame-Options"));
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").First());
    }

    [Fact]
    public async Task AllResponses_IncludeReferrerPolicy()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/.well-known/openid-configuration", ct);

        Assert.True(response.Headers.Contains("Referrer-Policy"));
        Assert.Equal("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").First());
    }

    [Fact]
    public async Task AllResponses_IncludeContentSecurityPolicy()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/.well-known/openid-configuration", ct);

        Assert.True(response.Headers.Contains("Content-Security-Policy"));
        var csp = response.Headers.GetValues("Content-Security-Policy").First();
        Assert.Contains("default-src 'self'", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.DoesNotContain("unsafe-eval", csp);
    }

    [Fact]
    public async Task AllResponses_IncludePermissionsPolicy()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/.well-known/openid-configuration", ct);

        Assert.True(response.Headers.Contains("Permissions-Policy"));
    }
}
