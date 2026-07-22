namespace RufistoWard.Core.Tests.Commands;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RufistoWard.Core.Commands;
using RufistoWard.Identity.Interfaces;
using RufistoWard.Tokens.Persistence;

/// <summary>
/// The environment guard on <see cref="SeedConformanceCommand"/> (Story 10.5b).
///
/// The fixtures this command seeds use FIXED credentials that are now PUBLISHED in the OIDF
/// certification logs, and the client is seeded with <c>RequirePkce=false</c>. Seeding them into a
/// real database hands anyone a working confidential client, so the command must refuse to run
/// unless the host is explicitly in the Conformance environment.
/// </summary>
[Trait("Category", "Conformance")]
public sealed class SeedConformanceGuardTests
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

    /// <param name="environment">
    /// Host environment. Note the deliberate asymmetry with the Conformance case: a developer may
    /// have a local, git-ignored <c>appsettings.Conformance.json</c> in the Host content root (the
    /// Story 10.5 runbook creates one), and the test host WILL load it once the environment matches.
    /// That file sets <c>Database:AutoMigrateOnStartup=true</c>, which registers a hosted service
    /// that calls relational <c>MigrateAsync</c> against this InMemory provider and throws. Pinning
    /// the flag via UseSetting lands in configuration before Program reads it, so the hosted service
    /// is never registered. Without this, the suite passes or fails depending on whether the
    /// developer happens to have run a conformance session.
    /// </param>
    private static WebApplicationFactory<Program> CreateFactory(string environment)
    {
        var dbName = $"SeedConformanceGuard-{Guid.NewGuid()}";
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(environment);
                builder.UseSetting("Database:AutoMigrateOnStartup", "false");
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

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("conformance")]   // case-sensitive on purpose — IsEnvironment is ordinal-ignore-case,
                                  // so this one SHOULD be accepted; asserted separately below.
    public async Task Seed_RefusesToRun_OutsideConformanceEnvironment(string environment)
    {
        if (string.Equals(environment, "conformance", StringComparison.OrdinalIgnoreCase))
        {
            return; // covered by Seed_Runs_InConformanceEnvironment
        }

        using var factory = CreateFactory(environment);

        var exitCode = await SeedConformanceCommand.ExecuteAsync(factory.Services, []);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task Seed_RefusesToRun_AndSeedsNothing_InProduction()
    {
        using var factory = CreateFactory("Production");

        var exitCode = await SeedConformanceCommand.ExecuteAsync(factory.Services, []);
        Assert.Equal(1, exitCode);

        // The point of the guard: refusing must mean NOTHING was written. An exit code alone
        // would still be a breach if the command had already seeded before returning.
        var ct = TestContext.Current.CancellationToken;
        using var scope = factory.Services.CreateScope();
        var clients = scope.ServiceProvider.GetRequiredService<IClientRepository>();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        Assert.Null(await clients.FindByClientIdAsync(SeedConformanceCommand.ConformanceClientId, ct));
        Assert.Null(await clients.FindByClientIdAsync(SeedConformanceCommand.SecondClientId, ct));
        Assert.Null(await users.FindByEmailAsync(SeedConformanceCommand.TestUserEmail, ct));
    }

    [Fact]
    public async Task Seed_Runs_InConformanceEnvironment()
    {
        using var factory = CreateFactory("Conformance");

        var exitCode = await SeedConformanceCommand.ExecuteAsync(factory.Services, []);
        Assert.Equal(0, exitCode);

        var ct = TestContext.Current.CancellationToken;
        using var scope = factory.Services.CreateScope();
        var clients = scope.ServiceProvider.GetRequiredService<IClientRepository>();
        Assert.NotNull(await clients.FindByClientIdAsync(SeedConformanceCommand.ConformanceClientId, ct));
    }

    [Fact]
    public async Task Seed_EnvironmentMatch_IsCaseInsensitive()
    {
        // ASPNETCORE_ENVIRONMENT is conventionally compared case-insensitively (IHostEnvironment
        // .IsEnvironment uses OrdinalIgnoreCase). A guard that used == would reject a legitimate
        // lowercase value and send someone hunting for a bug that isn't there.
        using var factory = CreateFactory("conformance");

        var exitCode = await SeedConformanceCommand.ExecuteAsync(factory.Services, []);

        Assert.Equal(0, exitCode);
    }
}
