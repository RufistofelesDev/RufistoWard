namespace RufistoWard.Core.Tests.Conformance;

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RufistoWard.Tokens.Persistence;

[Trait("Category", "Conformance")]
public sealed class DiscoveryDocumentConformanceTests
{
    private static readonly Dictionary<string, string?> TestConfig = new()
    {
        ["Server:BehindReverseProxy"] = "false",
        ["Database:ConnectionString"] = "unused",
        ["Token:Issuer"] = "https://idp.example.com",
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
                    foreach (var d in efDescriptors) services.Remove(d);
                    services.AddDbContext<RufistoWardDbContext>(options =>
                        options.UseInMemoryDatabase($"Conformance-{Guid.NewGuid()}"));
                });
            });
    }

    [Fact]
    public async Task Discovery_IssuerMatchesConfiguration()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/.well-known/openid-configuration", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(content);
        Assert.Equal("https://idp.example.com", doc.RootElement.GetProperty("issuer").GetString());
    }

    [Fact]
    public async Task Discovery_AllEndpointUrisUseIssuerPrefix()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var content = await (await client.GetAsync("/.well-known/openid-configuration", ct))
            .Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        var issuer = root.GetProperty("issuer").GetString()!;
        string[] endpointFields = ["authorization_endpoint", "token_endpoint", "userinfo_endpoint",
            "jwks_uri", "end_session_endpoint", "revocation_endpoint", "introspection_endpoint",
            "device_authorization_endpoint"];

        foreach (var field in endpointFields)
        {
            var uri = root.GetProperty(field).GetString();
            Assert.StartsWith(issuer, uri!, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Discovery_SupportsCodeResponseType()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var content = await (await client.GetAsync("/.well-known/openid-configuration", ct))
            .Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(content);

        var responseTypes = doc.RootElement.GetProperty("response_types_supported");
        var hasCode = false;
        foreach (var rt in responseTypes.EnumerateArray())
        {
            if (rt.GetString() == "code") hasCode = true;
        }
        Assert.True(hasCode, "response_types_supported must include 'code'");
    }

    [Fact]
    public async Task Discovery_AdvertisesQueryAndFormPostResponseModes()
    {
        // Absent, response_modes_supported defaults to ["query", "fragment"] — neither of which is
        // what RufistoWard actually supports. State it explicitly.
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var content = await (await client.GetAsync("/.well-known/openid-configuration", ct))
            .Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(content);

        var modes = doc.RootElement.GetProperty("response_modes_supported")
            .EnumerateArray().Select(m => m.GetString()!).ToArray();
        Assert.Equal(["query", "form_post"], modes);
    }

    [Fact]
    public async Task Discovery_SupportsS256CodeChallengeMethod()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var content = await (await client.GetAsync("/.well-known/openid-configuration", ct))
            .Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(content);

        var methods = doc.RootElement.GetProperty("code_challenge_methods_supported");
        var hasS256 = false;
        foreach (var m in methods.EnumerateArray())
        {
            if (m.GetString() == "S256") hasS256 = true;
        }
        Assert.True(hasS256, "code_challenge_methods_supported must include 'S256'");
    }

    [Fact]
    public async Task Jwks_ReturnsValidStructure()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/.well-known/jwks", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.TryGetProperty("keys", out var keys));
        Assert.Equal(JsonValueKind.Array, keys.ValueKind);
    }

    [Fact]
    public async Task EndSession_LogsOutAndReturnsOk()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/connect/endsession", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
