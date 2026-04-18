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
using RufistoWard.Identity.Entities;
using RufistoWard.Identity.Interfaces;
using RufistoWard.Tokens.Persistence;

// Negative-path tests for JWT validation. Each test crafts a specific attack token
// and asserts that the server rejects it with 401 + invalid_token.
// The target endpoint is /connect/userinfo because it requires a valid access token
// and has a concrete resource owner lookup, giving a clean rejection path.
[Trait("Category", "Security")]
public sealed class JwtNegativeTests : IAsyncLifetime
{
    private const string Issuer = "https://localhost:5001";
    private const string TestSubjectId = "test-subject-123";
    private const string TestAudience = "test-client";

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

                    var dbName = $"JwtNegativeTests-{Guid.NewGuid()}";
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

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RufistoWardDbContext>();
        await db.Database.EnsureCreatedAsync();

        var user = User.Create(TestSubjectId, "testuser", "test@example.com", "hashed");
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        _serverSigningKey.Dispose();
    }

    [Fact]
    public async Task AlgNone_UnsignedToken_IsRejected()
    {
        var header = Header(alg: "none");
        var payload = DefaultPayload();
        var token = $"{Encode(header)}.{Encode(payload)}.";  // empty signature segment

        var response = await SendAuthenticated(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("invalid_token", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task AlgorithmConfusion_RS256PayloadAsHS256_IsRejected()
    {
        // Attacker takes the server's RSA public key and uses it as an HMAC secret,
        // then submits a token with HS256 header. A naive validator that looks up
        // the configured key without checking alg-vs-key-type will validate it.
        var publicKeyBytes = _serverSigningKey.ExportSubjectPublicKeyInfo();
        var header = Header(alg: "HS256", kid: _serverKeyId);
        var payload = DefaultPayload();

        var signingInput = $"{Encode(header)}.{Encode(payload)}";
        using var hmac = new HMACSHA256(publicKeyBytes);
        var signature = hmac.ComputeHash(Encoding.UTF8.GetBytes(signingInput));
        var token = $"{signingInput}.{Base64UrlEncode(signature)}";

        var response = await SendAuthenticated(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("invalid_token", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task UnknownKid_IsRejected()
    {
        // Token signed with an attacker-controlled RSA key, header advertises a
        // kid that is not in the server's keyring.
        using var attackerKey = RSA.Create(2048);
        var header = Header(alg: "RS256", kid: "attacker-kid-does-not-exist");
        var payload = DefaultPayload();
        var token = SignRs256(attackerKey, header, payload);

        var response = await SendAuthenticated(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("invalid_token", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task JwkSpoofing_AttackerEmbedsOwnJwk_IsRejected()
    {
        // Attacker signs with their own RSA key and embeds the matching JWK in the
        // header. A naive validator that trusts header.jwk for signature verification
        // will accept it. The server MUST use its own key store (by kid) and ignore
        // any jwk embedded in the header.
        using var attackerKey = RSA.Create(2048);
        var attackerParams = attackerKey.ExportParameters(includePrivateParameters: false);

        var header = new Dictionary<string, object>
        {
            ["alg"] = "RS256",
            ["typ"] = "JWT",
            ["kid"] = _serverKeyId,  // use server's kid to pass lookup
            ["jwk"] = new Dictionary<string, string>
            {
                ["kty"] = "RSA",
                ["n"] = Base64UrlEncode(TrimLeadingZero(attackerParams.Modulus!)),
                ["e"] = Base64UrlEncode(TrimLeadingZero(attackerParams.Exponent!)),
            },
        };
        var payload = DefaultPayload();
        var token = SignRs256(attackerKey, header, payload);

        var response = await SendAuthenticated(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("invalid_token", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task Expired_ExpInPast_IsRejected()
    {
        var now = DateTimeOffset.UtcNow;
        var payload = DefaultPayload();
        payload["exp"] = now.AddSeconds(-300).ToUnixTimeSeconds();  // expired 5 min ago
        var token = SignRs256(_serverSigningKey, Header(), payload);

        var response = await SendAuthenticated(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("invalid_token", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task NotYetValid_NbfInFuture_IsRejected()
    {
        var now = DateTimeOffset.UtcNow;
        var payload = DefaultPayload();
        payload["nbf"] = now.AddSeconds(600).ToUnixTimeSeconds();  // valid in 10 min
        payload["iat"] = now.AddSeconds(600).ToUnixTimeSeconds();
        payload["exp"] = now.AddSeconds(4200).ToUnixTimeSeconds();
        var token = SignRs256(_serverSigningKey, Header(), payload);

        var response = await SendAuthenticated(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("invalid_token", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task WrongIssuer_IsRejected()
    {
        var payload = DefaultPayload();
        payload["iss"] = "https://evil.example.com";
        var token = SignRs256(_serverSigningKey, Header(), payload);

        var response = await SendAuthenticated(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("invalid_token", response.Headers.WwwAuthenticate.ToString());
    }

    // Audience validation deferred to Story 10.2a — requires IClientRepository
    // integration on AccessTokenValidator to verify aud is a registered client.
    // Test harness is ready; unskip when 10.2a lands.
    [Fact(Skip = "Deferred to Story 10.2a: aud validation via IClientRepository")]
    public async Task WrongAudience_IsRejected()
    {
        var payload = DefaultPayload();
        payload["aud"] = "wrong-audience";
        var token = SignRs256(_serverSigningKey, Header(), payload);

        var response = await SendAuthenticated(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("invalid_token", response.Headers.WwwAuthenticate.ToString());
    }

    // ---- helpers ----

    private Dictionary<string, object> Header(string alg = "RS256", string? kid = null)
        => new()
        {
            ["alg"] = alg,
            ["typ"] = "JWT",
            ["kid"] = kid ?? _serverKeyId,
        };

    private Dictionary<string, object> DefaultPayload()
    {
        var now = DateTimeOffset.UtcNow;
        return new Dictionary<string, object>
        {
            ["iss"] = Issuer,
            ["sub"] = TestSubjectId,
            ["aud"] = TestAudience,
            ["exp"] = now.AddSeconds(3600).ToUnixTimeSeconds(),
            ["iat"] = now.ToUnixTimeSeconds(),
            ["jti"] = Guid.NewGuid().ToString("N"),
            ["scope"] = "openid",
        };
    }

    private async Task<HttpResponseMessage> SendAuthenticated(string token)
    {
        var ct = TestContext.Current.CancellationToken;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.GetAsync("/connect/userinfo", ct);
    }

    private static string SignRs256(RSA key, IDictionary<string, object> header, IDictionary<string, object> payload)
    {
        var signingInput = $"{Encode(header)}.{Encode(payload)}";
        var signature = key.SignData(
            Encoding.UTF8.GetBytes(signingInput),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        return $"{signingInput}.{Base64UrlEncode(signature)}";
    }

    private static string Encode(IDictionary<string, object> dict)
        => Base64UrlEncode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(dict)));

    private static string Base64UrlEncode(byte[] data)
        => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] TrimLeadingZero(byte[] data)
        => data.Length > 0 && data[0] == 0 ? data[1..] : data;

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
