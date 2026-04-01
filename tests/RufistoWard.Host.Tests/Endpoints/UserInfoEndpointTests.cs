namespace RufistoWard.Core.Tests.Endpoints;

using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RufistoWard.Identity.Entities;
using RufistoWard.Identity.Interfaces;
using RufistoWard.Tokens.Persistence;

[Trait("Category", "Integration")]
public sealed class UserInfoEndpointTests : IAsyncLifetime
{
    private const string Issuer = "https://localhost:5001";
    private const string TestSubjectId = "test-subject-123";
    private const string TestUsername = "testuser";
    private const string TestEmail = "test@example.com";

    private static readonly Dictionary<string, string?> TestConfig = new()
    {
        ["Server:BehindReverseProxy"] = "false",
        ["Database:ConnectionString"] = "unused",
        ["Token:Issuer"] = Issuer,
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

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private RSA _signingKey = null!;
    private string _keyId = null!;

    public async ValueTask InitializeAsync()
    {
        _signingKey = RSA.Create(2048);
        _keyId = Guid.NewGuid().ToString("N")[..8];

        _factory = new WebApplicationFactory<Program>()
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

                    var dbName = $"UserInfoTests-{Guid.NewGuid()}";
                    services.AddDbContext<RufistoWardDbContext>(options =>
                        options.UseInMemoryDatabase(dbName));

                    // Replace ISigningKeyRepository with one that uses our test key
                    var signingKeyDescriptors = services
                        .Where(d => d.ServiceType == typeof(ISigningKeyRepository))
                        .ToList();
                    foreach (var d in signingKeyDescriptors)
                    {
                        services.Remove(d);
                    }
                    services.AddSingleton<ISigningKeyRepository>(
                        new TestSigningKeyRepository(_signingKey, _keyId));
                });
            });

        _client = _factory.CreateClient();

        // Seed a test user with claims
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RufistoWardDbContext>();
        await db.Database.EnsureCreatedAsync();

        var user = User.Create(TestSubjectId, TestUsername, TestEmail, "hashed-password");
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();

        // Add user claims via EF change tracker (UserClaim has private constructor)
        var nameEntry = db.UserClaims.Add(Activator.CreateInstance(typeof(UserClaim), nonPublic: true) as UserClaim
            ?? throw new InvalidOperationException());
        nameEntry.Property("UserId").CurrentValue = user.Id;
        nameEntry.Property("Type").CurrentValue = "name";
        nameEntry.Property("Value").CurrentValue = "Test User";

        var prefEntry = db.UserClaims.Add(Activator.CreateInstance(typeof(UserClaim), nonPublic: true) as UserClaim
            ?? throw new InvalidOperationException());
        prefEntry.Property("UserId").CurrentValue = user.Id;
        prefEntry.Property("Type").CurrentValue = "preferred_username";
        prefEntry.Property("Value").CurrentValue = "testuser";

        await db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        _signingKey.Dispose();
    }

    [Fact]
    public async Task UserInfo_ValidToken_OpenIdScope_ReturnsSub()
    {
        var ct = TestContext.Current.CancellationToken;
        var token = BuildAccessToken(TestSubjectId, ["openid"], 3600);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.GetAsync("/connect/userinfo", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        Assert.Equal(TestSubjectId, json.RootElement.GetProperty("sub").GetString());
        Assert.False(json.RootElement.TryGetProperty("email", out _));
        Assert.False(json.RootElement.TryGetProperty("name", out _));
    }

    [Fact]
    public async Task UserInfo_ValidToken_ProfileAndEmailScopes_ReturnsFullClaims()
    {
        var ct = TestContext.Current.CancellationToken;
        var token = BuildAccessToken(TestSubjectId, ["openid", "profile", "email"], 3600);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.GetAsync("/connect/userinfo", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        Assert.Equal(TestSubjectId, json.RootElement.GetProperty("sub").GetString());
        Assert.Equal(TestUsername, json.RootElement.GetProperty("preferred_username").GetString());
        Assert.Equal(TestEmail, json.RootElement.GetProperty("email").GetString());
        Assert.False(json.RootElement.GetProperty("email_verified").GetBoolean());
    }

    [Fact]
    public async Task UserInfo_MissingAuthorizationHeader_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync("/connect/userinfo", ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("Bearer", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task UserInfo_ExpiredToken_Returns401InvalidToken()
    {
        var ct = TestContext.Current.CancellationToken;
        var token = BuildAccessToken(TestSubjectId, ["openid"], -3600); // expired 1 hour ago

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.GetAsync("/connect/userinfo", ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("invalid_token", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task UserInfo_MalformedToken_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.valid.jwt");

        var response = await _client.GetAsync("/connect/userinfo", ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("invalid_token", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task UserInfo_MissingOpenIdScope_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var token = BuildAccessToken(TestSubjectId, ["profile"], 3600); // no openid scope

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.GetAsync("/connect/userinfo", ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("insufficient_scope", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task UserInfo_NonExistentUser_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;
        var token = BuildAccessToken("nonexistent-sub", ["openid"], 3600);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.GetAsync("/connect/userinfo", ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("invalid_token", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task UserInfo_PostMethod_WorksIdenticallyToGet()
    {
        var ct = TestContext.Current.CancellationToken;
        var token = BuildAccessToken(TestSubjectId, ["openid", "profile"], 3600);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.PostAsync("/connect/userinfo", null, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        Assert.Equal(TestSubjectId, json.RootElement.GetProperty("sub").GetString());
        Assert.Equal(TestUsername, json.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task UserInfo_InvalidSignature_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        // Build a token and tamper with the payload
        var token = BuildAccessToken(TestSubjectId, ["openid"], 3600);
        var parts = token.Split('.');
        // Flip a byte in the signature
        var sigBytes = Base64UrlDecode(parts[2]);
        sigBytes[0] ^= 0xFF;
        var tamperedToken = $"{parts[0]}.{parts[1]}.{Base64UrlEncode(sigBytes)}";

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tamperedToken);
        var response = await _client.GetAsync("/connect/userinfo", ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("invalid_token", response.Headers.WwwAuthenticate.ToString());
    }

    private string BuildAccessToken(string subject, string[] scopes, int lifetimeSeconds)
    {
        var now = DateTimeOffset.UtcNow;
        var header = new Dictionary<string, string>
        {
            ["alg"] = "RS256",
            ["typ"] = "JWT",
            ["kid"] = _keyId,
        };
        var payload = new Dictionary<string, object>
        {
            ["iss"] = Issuer,
            ["sub"] = subject,
            ["aud"] = "test-client",
            ["exp"] = now.AddSeconds(lifetimeSeconds).ToUnixTimeSeconds(),
            ["iat"] = now.ToUnixTimeSeconds(),
            ["jti"] = Guid.NewGuid().ToString("N"),
            ["scope"] = string.Join(' ', scopes),
        };

        var headerJson = JsonSerializer.Serialize(header);
        var payloadJson = JsonSerializer.Serialize(payload);
        var headerB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
        var payloadB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));

        var signingInput = Encoding.UTF8.GetBytes($"{headerB64}.{payloadB64}");
        var signature = _signingKey.SignData(signingInput, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return $"{headerB64}.{payloadB64}.{Base64UrlEncode(signature)}";
    }

    private static string Base64UrlEncode(byte[] data)
        => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        return Convert.FromBase64String(padded);
    }

    private sealed class TestSigningKeyRepository(RSA rsa, string keyId) : ISigningKeyRepository
    {
        private readonly SigningKey _key = CreateTestKey(rsa, keyId);

        public Task<SigningKey?> FindByKeyIdAsync(string kid, CancellationToken cancellationToken = default)
            => Task.FromResult<SigningKey?>(kid == keyId ? _key : null);

        public Task<SigningKey?> GetActiveKeyAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<SigningKey?>(_key);

        public Task<IReadOnlyList<SigningKey>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SigningKey>>([_key]);

        public Task AddAsync(SigningKey key, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpdateAsync(SigningKey key, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        private static SigningKey CreateTestKey(RSA rsa, string kid)
        {
            var serialized = Convert.ToBase64String(rsa.ExportRSAPrivateKey());
            return SigningKey.Create(kid, "RS256", "RSA", serialized);
        }
    }
}
