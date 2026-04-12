namespace RufistoWard.Core.Tests.Endpoints;

using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RufistoWard.Tokens.Persistence;

[Trait("Category", "Integration")]
public sealed class AdminApiTests
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
        var dbName = $"AdminTests-{Guid.NewGuid()}";
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
                        options.UseInMemoryDatabase(dbName));

                    services.AddAuthentication(defaultScheme: "TestScheme")
                        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestScheme", _ => { });
                });
            });
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[] { new Claim(ClaimTypes.Name, "admin"), new Claim(ClaimTypes.Role, "Admin") };
            var identity = new ClaimsIdentity(claims, "TestScheme");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "TestScheme");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    // ==================== CLIENT CRUD ====================

    [Fact]
    public async Task Clients_ListEmpty_Returns200()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/admin/clients", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal(0, doc.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Clients_GetNonexistent_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/admin/clients/nonexistent", ct);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Clients_Create_Returns201WithClientIdAndSecret()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/admin/clients", new
        {
            clientName = "Test App",
            grantTypes = new[] { "authorization_code" },
            redirectUris = new[] { "https://example.com/callback" },
            allowedScopes = new[] { "openid", "profile" },
            requireClientSecret = true,
        }, ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.False(string.IsNullOrEmpty(root.GetProperty("clientId").GetString()));
        Assert.False(string.IsNullOrEmpty(root.GetProperty("clientSecret").GetString()));
        Assert.Equal("Test App", root.GetProperty("clientName").GetString());
        Assert.True(root.GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public async Task Clients_CreateThenGet_ReturnsCreatedClient()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/admin/clients", new
        {
            clientName = "Roundtrip App",
            grantTypes = new[] { "client_credentials" },
            redirectUris = Array.Empty<string>(),
            allowedScopes = new[] { "api" },
            requireClientSecret = true,
        }, ct);

        var createBody = await createResponse.Content.ReadAsStringAsync(ct);
        using var createDoc = JsonDocument.Parse(createBody);
        var clientId = createDoc.RootElement.GetProperty("clientId").GetString()!;

        var getResponse = await client.GetAsync($"/api/admin/clients/{clientId}", ct);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var getBody = await getResponse.Content.ReadAsStringAsync(ct);
        using var getDoc = JsonDocument.Parse(getBody);
        Assert.Equal("Roundtrip App", getDoc.RootElement.GetProperty("clientName").GetString());
    }

    [Fact]
    public async Task Clients_Update_ReturnsUpdatedClient()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/admin/clients", new
        {
            clientName = "Original Name",
            grantTypes = new[] { "authorization_code" },
            redirectUris = Array.Empty<string>(),
            allowedScopes = Array.Empty<string>(),
            requireClientSecret = false,
        }, ct);
        var createBody = await createResponse.Content.ReadAsStringAsync(ct);
        using var createDoc = JsonDocument.Parse(createBody);
        var clientId = createDoc.RootElement.GetProperty("clientId").GetString()!;

        var updateResponse = await client.PutAsJsonAsync($"/api/admin/clients/{clientId}", new
        {
            clientName = "Updated Name",
            enabled = false,
        }, ct);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updateBody = await updateResponse.Content.ReadAsStringAsync(ct);
        using var updateDoc = JsonDocument.Parse(updateBody);
        Assert.Equal("Updated Name", updateDoc.RootElement.GetProperty("clientName").GetString());
        Assert.False(updateDoc.RootElement.GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public async Task Clients_Delete_Returns204()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/admin/clients", new
        {
            clientName = "To Delete",
            grantTypes = Array.Empty<string>(),
            redirectUris = Array.Empty<string>(),
            allowedScopes = Array.Empty<string>(),
            requireClientSecret = false,
        }, ct);
        var createBody = await createResponse.Content.ReadAsStringAsync(ct);
        using var createDoc = JsonDocument.Parse(createBody);
        var clientId = createDoc.RootElement.GetProperty("clientId").GetString()!;

        var deleteResponse = await client.DeleteAsync($"/api/admin/clients/{clientId}", ct);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await client.GetAsync($"/api/admin/clients/{clientId}", ct);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    // ==================== SCOPE CRUD ====================

    [Fact]
    public async Task Scopes_ListEmpty_Returns200()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/admin/scopes", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Scopes_Create_Returns201()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/admin/scopes", new
        {
            name = "api.read",
            displayName = "Read API",
            description = "Read access to the API",
        }, ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("api.read", doc.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Scopes_CreateDuplicate_Returns409()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/admin/scopes", new { name = "dup.scope" }, ct);
        var response = await client.PostAsJsonAsync("/api/admin/scopes", new { name = "dup.scope" }, ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Scopes_Update_ReturnsUpdatedScope()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/admin/scopes", new { name = "scope.upd" }, ct);
        var createBody = await createResponse.Content.ReadAsStringAsync(ct);
        using var createDoc = JsonDocument.Parse(createBody);
        var id = createDoc.RootElement.GetProperty("id").GetInt32();

        var updateResponse = await client.PutAsJsonAsync($"/api/admin/scopes/{id}", new
        {
            displayName = "Updated Display",
            enabled = false,
        }, ct);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var body = await updateResponse.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("Updated Display", doc.RootElement.GetProperty("displayName").GetString());
        Assert.False(doc.RootElement.GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public async Task Scopes_Delete_Returns204()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/admin/scopes", new { name = "scope.del" }, ct);
        var createBody = await createResponse.Content.ReadAsStringAsync(ct);
        using var createDoc = JsonDocument.Parse(createBody);
        var id = createDoc.RootElement.GetProperty("id").GetInt32();

        var deleteResponse = await client.DeleteAsync($"/api/admin/scopes/{id}", ct);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    // ==================== USER CRUD ====================

    [Fact]
    public async Task Users_ListEmpty_Returns200()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/admin/users", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Users_Create_Returns201()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/admin/users", new
        {
            username = "testuser",
            email = "test@example.com",
            password = "P@ssw0rd!",
        }, ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("testuser", doc.RootElement.GetProperty("username").GetString());
        Assert.True(doc.RootElement.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task Users_CreateDuplicateUsername_Returns409()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/admin/users", new
        {
            username = "dupuser", email = "a@example.com", password = "P@ss1!",
        }, ct);

        var response = await client.PostAsJsonAsync("/api/admin/users", new
        {
            username = "dupuser", email = "b@example.com", password = "P@ss2!",
        }, ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Users_Update_ReturnsUpdatedUser()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/admin/users", new
        {
            username = "updateme", email = "update@example.com", password = "P@ss1!",
        }, ct);
        var createBody = await createResponse.Content.ReadAsStringAsync(ct);
        using var createDoc = JsonDocument.Parse(createBody);
        var subjectId = createDoc.RootElement.GetProperty("subjectId").GetString()!;

        var updateResponse = await client.PutAsJsonAsync($"/api/admin/users/{subjectId}", new
        {
            username = "updated_user",
        }, ct);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var body = await updateResponse.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("updated_user", doc.RootElement.GetProperty("username").GetString());
    }

    [Fact]
    public async Task Users_DeactivateAndActivate_TogglesIsActive()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/admin/users", new
        {
            username = "toggleuser", email = "toggle@example.com", password = "P@ss1!",
        }, ct);
        var createBody = await createResponse.Content.ReadAsStringAsync(ct);
        using var createDoc = JsonDocument.Parse(createBody);
        var subjectId = createDoc.RootElement.GetProperty("subjectId").GetString()!;

        var deactivateResponse = await client.PostAsync($"/api/admin/users/{subjectId}/deactivate", null, ct);
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);
        var deactivateBody = await deactivateResponse.Content.ReadAsStringAsync(ct);
        using var deactivateDoc = JsonDocument.Parse(deactivateBody);
        Assert.False(deactivateDoc.RootElement.GetProperty("isActive").GetBoolean());

        var activateResponse = await client.PostAsync($"/api/admin/users/{subjectId}/activate", null, ct);
        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);
        var activateBody = await activateResponse.Content.ReadAsStringAsync(ct);
        using var activateDoc = JsonDocument.Parse(activateBody);
        Assert.True(activateDoc.RootElement.GetProperty("isActive").GetBoolean());
    }

    // ==================== ROLE CRUD ====================

    [Fact]
    public async Task Roles_ListEmpty_Returns200()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/admin/roles", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Roles_Create_Returns201WithPermissions()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/admin/roles", new
        {
            name = "admin",
            description = "Administrator role",
            permissions = new[] { "clients:read", "clients:write", "users:read" },
        }, ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("admin", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal(3, doc.RootElement.GetProperty("permissions").GetArrayLength());
    }

    [Fact]
    public async Task Roles_CreateDuplicate_Returns409()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/admin/roles", new { name = "duprole" }, ct);
        var response = await client.PostAsJsonAsync("/api/admin/roles", new { name = "duprole" }, ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Roles_Update_ReturnsUpdatedRole()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/admin/roles", new
        {
            name = "editor",
            permissions = new[] { "read" },
        }, ct);
        var createBody = await createResponse.Content.ReadAsStringAsync(ct);
        using var createDoc = JsonDocument.Parse(createBody);
        var id = createDoc.RootElement.GetProperty("id").GetInt32();

        var updateResponse = await client.PutAsJsonAsync($"/api/admin/roles/{id}", new
        {
            description = "Editor role",
            permissions = new[] { "read", "write" },
        }, ct);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var body = await updateResponse.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("Editor role", doc.RootElement.GetProperty("description").GetString());
        Assert.Equal(2, doc.RootElement.GetProperty("permissions").GetArrayLength());
    }

    [Fact]
    public async Task Roles_Delete_Returns204()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/admin/roles", new { name = "todelete" }, ct);
        var createBody = await createResponse.Content.ReadAsStringAsync(ct);
        using var createDoc = JsonDocument.Parse(createBody);
        var id = createDoc.RootElement.GetProperty("id").GetInt32();

        var deleteResponse = await client.DeleteAsync($"/api/admin/roles/{id}", ct);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    // ==================== API RESOURCES CRUD ====================

    [Fact]
    public async Task ApiResources_ListEmpty_Returns200()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/admin/api-resources", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ApiResources_Create_Returns201()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/admin/api-resources", new
        {
            name = "my-api",
            displayName = "My API",
            scopes = new[] { "api.read", "api.write" },
        }, ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("my-api", doc.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task ApiResources_Delete_Returns204()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/admin/api-resources", new
        {
            name = "del-api",
            scopes = Array.Empty<string>(),
        }, ct);
        var createBody = await createResponse.Content.ReadAsStringAsync(ct);
        using var createDoc = JsonDocument.Parse(createBody);
        var id = createDoc.RootElement.GetProperty("id").GetInt32();

        var deleteResponse = await client.DeleteAsync($"/api/admin/api-resources/{id}", ct);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }
}
