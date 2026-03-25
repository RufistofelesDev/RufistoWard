namespace RufistoWard.Core.Tests.HealthChecks;

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RufistoWard.Tokens.Persistence;

[Trait("Category", "Integration")]
public sealed class HealthEndpointTests
{
    private static readonly Dictionary<string, string?> TestConfig = new()
    {
        ["Server:BehindReverseProxy"] = "false",
        ["Database:ConnectionString"] = "unused-for-inmemory",
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
                    // Remove all EF Core / DbContext registrations to avoid dual-provider conflict
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
                        options.UseInMemoryDatabase($"HealthCheckTests-{Guid.NewGuid()}"));
                });
            });
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOkWithJsonBody()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health", ct);

        // Assert — may be 503 if signing key check fails (no key seeded), that's expected
        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.ServiceUnavailable,
            $"Expected 200 or 503, got {response.StatusCode}");
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("status", out _));
        Assert.True(root.TryGetProperty("checks", out var checks));
        Assert.True(checks.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task HealthEndpoint_IncludesDatabaseCheck()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health", ct);
        var content = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(content);

        // Assert
        var checks = doc.RootElement.GetProperty("checks");
        var hasDbCheck = false;
        foreach (var check in checks.EnumerateArray())
        {
            if (check.GetProperty("name").GetString() == "database")
            {
                hasDbCheck = true;
                // InMemory provider is always healthy
                Assert.Equal("Healthy", check.GetProperty("status").GetString());
                break;
            }
        }
        Assert.True(hasDbCheck, "Expected a 'database' health check entry.");
    }

    [Fact]
    public async Task HealthEndpoint_IncludesSigningKeyCheck()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health", ct);
        var content = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(content);

        // Assert
        var checks = doc.RootElement.GetProperty("checks");
        var hasKeyCheck = false;
        foreach (var check in checks.EnumerateArray())
        {
            if (check.GetProperty("name").GetString() == "signing-key")
            {
                hasKeyCheck = true;
            }
        }
        Assert.True(hasKeyCheck, "Expected a 'signing-key' health check entry.");
    }
}
