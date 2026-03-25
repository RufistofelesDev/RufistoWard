namespace RufistoWard.Core.Tests.Endpoints;

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RufistoWard.Tokens.Persistence;

[Trait("Category", "Integration")]
public sealed class DeviceEndpointTests
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
                        options.UseInMemoryDatabase($"DeviceTests-{Guid.NewGuid()}"));
                });
            });
    }

    [Fact]
    public async Task DeviceAuthorization_InvalidClient_ReturnsError()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var content = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("client_id", "nonexistent"),
            new KeyValuePair<string, string>("scope", "openid"),
        ]);

        var response = await client.PostAsync("/connect/deviceauthorization", content, ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("invalid_client", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task DeviceAuthorization_MissingClientId_ReturnsError()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var content = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("scope", "openid"),
        ]);

        var response = await client.PostAsync("/connect/deviceauthorization", content, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DevicePage_Renders()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/device", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync(ct);
        Assert.Contains("data-testid=\"device-form\"", content);
        Assert.Contains("data-testid=\"user-code-input\"", content);
    }

    [Fact]
    public async Task TokenEndpoint_DeviceCodeWithoutCode_ReturnsError()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var content = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("grant_type", "urn:ietf:params:oauth:grant-type:device_code"),
            new KeyValuePair<string, string>("client_id", "test"),
        ]);

        var response = await client.PostAsync("/connect/token", content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        var error = doc.RootElement.GetProperty("error").GetString();
        Assert.True(error is "invalid_request" or "invalid_client",
            $"Expected invalid_request or invalid_client, got {error}");
    }
}
