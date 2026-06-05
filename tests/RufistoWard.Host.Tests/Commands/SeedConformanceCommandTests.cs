namespace RufistoWard.Core.Tests.Commands;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RufistoWard.Auth.Protocol.Validation;
using RufistoWard.Core.Commands;
using RufistoWard.Identity.Interfaces;
using RufistoWard.Tokens.Persistence;

[Trait("Category", "Conformance")]
public sealed class SeedConformanceCommandTests
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
        // Generate the DB name ONCE per factory and capture it: the AddDbContext options
        // lambda runs per scope, so a Guid created inside it would give every scope its own
        // database — and this test writes in one scope and reads back in another.
        var dbName = $"SeedConformance-{Guid.NewGuid()}";
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
                        services.Remove(d);
                    services.AddDbContext<RufistoWardDbContext>(options =>
                        options.UseInMemoryDatabase(dbName));
                });
            });
    }

    [Fact]
    public async Task Seed_CreatesClient_WithConformanceConfiguration()
    {
        using var factory = CreateFactory();

        var exitCode = await SeedConformanceCommand.ExecuteAsync(factory.Services, []);
        Assert.Equal(0, exitCode);

        using var scope = factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IClientRepository>();
        var client = await repo.FindByClientIdAsync(
            SeedConformanceCommand.ConformanceClientId, TestContext.Current.CancellationToken);

        Assert.NotNull(client);
        Assert.True(client.Enabled);
        Assert.True(client.RequirePkce);
        Assert.True(client.RequireClientSecret);

        var grantTypes = client.GrantTypes.Select(g => g.GrantType).ToArray();
        Assert.Contains("authorization_code", grantTypes);
        Assert.Contains("refresh_token", grantTypes);

        var scopes = client.Scopes.Select(s => s.Scope).ToArray();
        Assert.Contains("openid", scopes);
        Assert.Contains("profile", scopes);
        Assert.Contains("email", scopes);

        var redirectUris = client.RedirectUris.Select(r => r.Uri).ToArray();
        foreach (var expected in SeedConformanceCommand.RedirectUris)
        {
            Assert.Contains(expected, redirectUris);
        }
    }

    [Fact]
    public async Task Seed_ClientSecret_AuthenticatesAtTokenEndpoint()
    {
        using var factory = CreateFactory();
        await SeedConformanceCommand.ExecuteAsync(factory.Services, []);

        var ct = TestContext.Current.CancellationToken;
        using var scope = factory.Services.CreateScope();
        var authenticator = scope.ServiceProvider.GetRequiredService<IClientAuthenticator>();

        var good = await authenticator.AuthenticateAsync(
            SeedConformanceCommand.ConformanceClientId,
            SeedConformanceCommand.ConformanceClientSecret,
            ct);
        Assert.True(good.IsSuccess, "Seeded client must authenticate with the documented secret.");

        var bad = await authenticator.AuthenticateAsync(
            SeedConformanceCommand.ConformanceClientId,
            "wrong-secret",
            ct);
        Assert.False(bad.IsSuccess, "A wrong secret must not authenticate.");
    }

    [Fact]
    public async Task Seed_CreatesLoginableTestUser_WithVerifiedEmail()
    {
        using var factory = CreateFactory();
        await SeedConformanceCommand.ExecuteAsync(factory.Services, []);

        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var user = await users.FindByEmailAsync(
            SeedConformanceCommand.TestUserEmail, TestContext.Current.CancellationToken);
        Assert.NotNull(user);
        Assert.Equal(SeedConformanceCommand.TestUsername, user.Username);
        Assert.True(user.EmailConfirmed, "email_verified must be true for the email-scope claim.");
        Assert.True(user.IsActive);

        var verification = hasher.Verify(user.PasswordHash, SeedConformanceCommand.TestUserPassword);
        Assert.Equal(PasswordVerificationResult.Success, verification);
    }

    [Fact]
    public async Task Seed_IsIdempotent_OnRepeatedRuns()
    {
        using var factory = CreateFactory();

        Assert.Equal(0, await SeedConformanceCommand.ExecuteAsync(factory.Services, []));
        Assert.Equal(0, await SeedConformanceCommand.ExecuteAsync(factory.Services, []));

        using var scope = factory.Services.CreateScope();
        var clients = scope.ServiceProvider.GetRequiredService<IClientRepository>();
        var client = await clients.FindByClientIdAsync(
            SeedConformanceCommand.ConformanceClientId, TestContext.Current.CancellationToken);

        Assert.NotNull(client);
        // No duplicate secrets/redirects from the second run.
        Assert.Single(client.Secrets);
        Assert.Equal(SeedConformanceCommand.RedirectUris.Length, client.RedirectUris.Count);
    }
}
