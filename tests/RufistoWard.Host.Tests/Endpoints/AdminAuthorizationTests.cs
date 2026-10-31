namespace RufistoWard.Core.Tests.Endpoints;

using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RufistoWard.Core.Bootstrap;
using RufistoWard.Identity.Entities;
using RufistoWard.Identity.Interfaces;
using RufistoWard.Tokens.Persistence;

/// <summary>
/// Before v1.0-rc2 the admin UI had no authorization at all and the admin API accepted any signed-in
/// user: an anonymous visitor could create OAuth clients and users. These tests go through the real
/// login page and the real cookie, which is the path an attacker uses.
/// </summary>
[Trait("Category", "Integration")]
public sealed partial class AdminAuthorizationTests
{
    private const string Password = "Correct-Horse-9";
    private const string AdminEmail = "admin@rufistoward.test";
    private const string UserEmail = "user@rufistoward.test";

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

    public static TheoryData<string> AdminPages =>
        ["/admin", "/admin/users", "/admin/users/new", "/admin/apps", "/admin/apps/new", "/admin/roles", "/admin/roles/new", "/admin/scopes", "/admin/scopes/new"];

    public static TheoryData<string> AdminApi =>
        ["/api/admin/users", "/api/admin/clients", "/api/admin/roles", "/api/admin/scopes"];

    [Theory]
    [MemberData(nameof(AdminPages))]
    public async Task Anonymous_AdminPage_RedirectsToLogin(string path)
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory);

        var response = await client.GetAsync(path, ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("https://localhost/login", response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(AdminApi))]
    public async Task Anonymous_AdminApi_Returns401(string path)
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory);

        var response = await client.GetAsync(path, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotCreateAClient()
    {
        // The exact request that worked against v1.0-rc1: POST the new-app form with no session.
        var ct = TestContext.Current.CancellationToken;
        using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory);

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["clientName"] = "anon-probe",
            ["grantTypes"] = "authorization_code",
            ["redirectUris"] = "https://attacker.example/cb",
            ["allowedScopes"] = "openid",
        });
        var response = await client.PostAsync("/admin/apps/new", form, ct);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RufistoWardDbContext>();
        Assert.False(await db.Clients.AnyAsync(c => c.ClientName == "anon-probe", ct));
    }

    [Fact]
    public async Task SignedInNonAdmin_IsForbidden()
    {
        // Every end user of every client signs in to this same cookie. Signed in is not admin.
        var ct = TestContext.Current.CancellationToken;
        using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory);
        await SignInAsync(client, UserEmail, ct);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/admin/users", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/users", ct)).StatusCode);
    }

    [Fact]
    public async Task Admin_IsAllowed()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory);
        await SignInAsync(client, AdminEmail, ct);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/admin/users", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/users", ct)).StatusCode);
    }

    [Fact]
    public async Task AdminDeactivatedMidSession_LosesAccessOnTheNextRequest()
    {
        // The role is checked against the database per request, not stamped into the sliding cookie.
        var ct = TestContext.Current.CancellationToken;
        using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory);
        await SignInAsync(client, AdminEmail, ct);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/admin/users", ct)).StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RufistoWardDbContext>();
            var admin = await db.Users.SingleAsync(u => u.Email == AdminEmail, ct);
            admin.Deactivate();
            await db.SaveChangesAsync(ct);
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/admin/users", ct)).StatusCode);
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory)
        => factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            // The session cookie is Secure-only, so the test client must speak https to keep it.
            BaseAddress = new Uri("https://localhost"),
        });

    private static async Task SignInAsync(HttpClient client, string email, CancellationToken ct)
    {
        var loginPage = await client.GetStringAsync("/login", ct);
        var token = AntiforgeryToken().Match(loginPage).Groups[1].Value;

        var response = await client.PostAsync("/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["usernameOrEmail"] = email,
            ["password"] = Password,
            ["__RequestVerificationToken"] = token,
        }), ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static async Task<WebApplicationFactory<Program>> CreateFactoryAsync()
    {
        var databaseName = $"AdminAuthzTests-{Guid.NewGuid()}";
        var factory = new WebApplicationFactory<Program>()
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

                    services.AddDbContext<RufistoWardDbContext>(options => options.UseInMemoryDatabase(databaseName));
                });
            });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RufistoWardDbContext>();
        var hash = scope.ServiceProvider.GetRequiredService<IPasswordHasher>().Hash(Password);

        var role = Role.Create(AdminAuthorization.RoleName);
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var admin = User.Create("admin-sub", "admin", AdminEmail, hash);
        admin.AssignRole(role.Id);
        db.Users.Add(admin);
        db.Users.Add(User.Create("user-sub", "user", UserEmail, hash));
        await db.SaveChangesAsync();

        return factory;
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryToken();
}
