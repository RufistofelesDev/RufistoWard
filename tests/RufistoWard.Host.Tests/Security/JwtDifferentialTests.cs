namespace RufistoWard.Core.Tests.Security;

using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using RufistoWard.Identity.Entities;
using RufistoWard.Identity.Interfaces;
using RufistoWard.Tokens.Persistence;

// Differential validation of server-issued tokens. The server issues a token
// through its real endpoint; the test parses and validates that token using
// Microsoft.IdentityModel.JsonWebTokens as a reference oracle.
// If the oracle cannot validate RufistoWard's token, RufistoWard's manual
// JWT construction has silently drifted from spec.
//
// Scope for Story 10.3: client_credentials grant (end-to-end proof of pattern).
// The other three grants (auth code + PKCE, refresh, device code) are deferred
// to Story 10.3a; same harness, more flow seeding.
[Trait("Category", "Security")]
public sealed class JwtDifferentialTests : IAsyncLifetime
{
    private const string Issuer = "https://localhost:5001";
    private const string TestClientId = "diff-test-client";
    private const string TestClientSecret = "super-secret-for-differential-test";
    private const string TestScope = "api";

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
    private RSA _serverSigningKey = null!;
    private string _serverKeyId = null!;

    public async ValueTask InitializeAsync()
    {
        _serverSigningKey = RSA.Create(2048);
        _serverKeyId = Guid.NewGuid().ToString("N")[..8];

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

                    var dbName = $"JwtDifferentialTests-{Guid.NewGuid()}";
                    services.AddDbContext<RufistoWardDbContext>(options =>
                        options.UseInMemoryDatabase(dbName));

                    var signingKeyDescriptors = services
                        .Where(d => d.ServiceType == typeof(ISigningKeyRepository))
                        .ToList();
                    foreach (var d in signingKeyDescriptors)
                    {
                        services.Remove(d);
                    }
                    services.AddSingleton<ISigningKeyRepository>(
                        new TestSigningKeyRepository(_serverSigningKey, _serverKeyId));
                });
            });

        _client = _factory.CreateClient();

        // Seed a confidential client with client_credentials grant.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RufistoWardDbContext>();
        await db.Database.EnsureCreatedAsync();

        var client = Client.Create(TestClientId, "Differential Test Client", requireClientSecret: true);
        var hashedSecret = Convert.ToBase64String(
            SHA256.HashData(Encoding.UTF8.GetBytes(TestClientSecret)));
        client.AddSecret(hashedSecret);
        client.AddScope(TestScope);
        client.AddGrantType("client_credentials");
        await db.Clients.AddAsync(client);
        await db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        _serverSigningKey.Dispose();
    }

    [Fact]
    public async Task ClientCredentials_IssuedAccessToken_ValidatesAgainstReferenceOracle()
    {
        var ct = TestContext.Current.CancellationToken;

        // Obtain a real access token from the real /connect/token endpoint.
        var request = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("client_id", TestClientId),
            new KeyValuePair<string, string>("client_secret", TestClientSecret),
            new KeyValuePair<string, string>("scope", TestScope),
        });
        var response = await _client.PostAsync("/connect/token", request, ct);

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Token endpoint returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(ct));
        var accessToken = doc.RootElement.GetProperty("access_token").GetString();
        Assert.False(string.IsNullOrEmpty(accessToken), "access_token missing from response");

        // Validate with the Microsoft.IdentityModel oracle.
        var oracleHandler = new JsonWebTokenHandler();
        var publicKey = new RsaSecurityKey(_serverSigningKey.ExportParameters(includePrivateParameters: false))
        {
            KeyId = _serverKeyId,
        };
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = false,  // RufistoWard does not require a specific aud on access tokens (see Story 10.2a)
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = publicKey,
            ClockSkew = TimeSpan.FromSeconds(30),
        };

        var result = await oracleHandler.ValidateTokenAsync(accessToken, validationParameters);

        Assert.True(result.IsValid,
            $"Oracle failed to validate server-issued token: {result.Exception?.Message}");

        // Claim parity: every claim the server advertised in the token should
        // come out of the oracle identically.
        var serverClaims = ParseServerClaims(accessToken!);
        foreach (var (key, expected) in serverClaims)
        {
            Assert.True(
                result.Claims.TryGetValue(key, out var actual),
                $"Oracle missing claim '{key}' that server advertised");
            Assert.Equal(expected, actual?.ToString());
        }

        // Spot-check the OIDC-required claims that matter most for an access token.
        Assert.Equal(Issuer, result.Claims["iss"].ToString());
        Assert.Equal(TestClientId, result.Claims["sub"].ToString());
        // RFC 9068 §2.2 makes client_id REQUIRED on JWT access tokens (Story 10.2b).
        Assert.True(
            result.Claims.TryGetValue("client_id", out var clientIdValue),
            "RFC 9068 §2.2 requires the client_id claim on access tokens");
        Assert.Equal(TestClientId, clientIdValue?.ToString());
    }

    private static Dictionary<string, string?> ParseServerClaims(string jwt)
    {
        var parts = jwt.Split('.');
        var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
        using var doc = JsonDocument.Parse(payloadJson);
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            // Normalize common numeric claims to string form matching the oracle.
            result[prop.Name] = prop.Value.ValueKind switch
            {
                JsonValueKind.String => prop.Value.GetString(),
                JsonValueKind.Number => prop.Value.GetRawText(),
                _ => prop.Value.GetRawText(),
            };
        }
        return result;
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2:
                padded += "==";
                break;
            case 3:
                padded += "=";
                break;
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
