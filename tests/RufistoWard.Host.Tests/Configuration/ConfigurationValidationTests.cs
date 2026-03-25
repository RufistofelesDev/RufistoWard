namespace RufistoWard.Core.Tests.Configuration;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RufistoWard.Tokens.Persistence;

[Trait("Category", "Unit")]
public sealed class ConfigurationValidationTests
{
    private static readonly Dictionary<string, string?> ValidConfig = new()
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

    private static WebApplicationFactory<Program> CreateFactory(Dictionary<string, string?> config)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, c) =>
                {
                    c.Sources.Clear();
                    c.AddInMemoryCollection(config);
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
                        options.UseInMemoryDatabase($"ConfigTests-{Guid.NewGuid()}"));
                });
            });
    }

    private static WebApplicationFactory<Program> CreateFactoryNoDb(Dictionary<string, string?> config)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, c) =>
                {
                    c.Sources.Clear();
                    c.AddInMemoryCollection(config);
                });
            });
    }

    [Fact]
    public void ValidConfiguration_ServerStartsSuccessfully()
    {
        var factory = CreateFactory(ValidConfig);
        using var client = factory.CreateClient();
    }

    [Fact]
    public void MissingRequiredTokenIssuer_StartupFails()
    {
        var config = new Dictionary<string, string?>(ValidConfig);
        config.Remove("Token:Issuer");

        var factory = CreateFactoryNoDb(config);
        var ex = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.Contains("Issuer", ex.Message);
    }

    [Fact]
    public void MissingRequiredDatabaseConnectionString_StartupFails()
    {
        var config = new Dictionary<string, string?>(ValidConfig);
        config.Remove("Database:ConnectionString");

        var factory = CreateFactoryNoDb(config);
        var ex = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.Contains("ConnectionString", ex.Message);
    }

    [Fact]
    public void InvalidAccessTokenLifetime_StartupFails()
    {
        var config = new Dictionary<string, string?>(ValidConfig)
        {
            ["Token:AccessTokenLifetimeSeconds"] = "10",
        };

        var factory = CreateFactoryNoDb(config);
        var ex = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.Contains("AccessTokenLifetimeSeconds", ex.Message);
    }

    [Fact]
    public void ReverseProxyWithoutKnownNetworks_StartupFails()
    {
        var config = new Dictionary<string, string?>(ValidConfig)
        {
            ["Server:BehindReverseProxy"] = "true",
        };

        var factory = CreateFactoryNoDb(config);
        var ex = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.Contains("KnownProxyNetworks", ex.Message);
    }

    [Fact]
    public void OidcModeWithoutAuthority_StartupFails()
    {
        var config = new Dictionary<string, string?>(ValidConfig)
        {
            ["AdminAuth:Mode"] = "OIDC",
        };

        var factory = CreateFactoryNoDb(config);
        var ex = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.Contains("OidcAuthority", ex.Message);
    }
}
