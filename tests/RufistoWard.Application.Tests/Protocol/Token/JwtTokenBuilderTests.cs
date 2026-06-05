namespace RufistoWard.Auth.Tests.Protocol.Token;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RufistoWard.Auth.Protocol.Token;
using RufistoWard.Identity.Entities;
using RufistoWard.Identity.Interfaces;

[Trait("Category", "Unit")]
public sealed class JwtTokenBuilderTests
{
    [Fact]
    public async Task BuildAccessToken_ProducesValidJwt()
    {
        var ct = TestContext.Current.CancellationToken;
        var repo = new FakeSigningKeyRepository();
        var builder = new JwtTokenBuilder(repo);

        var context = new TokenBuildContext
        {
            Subject = "user-123",
            ClientId = "test-client",
            Issuer = "https://localhost:5001",
            LifetimeSeconds = 3600,
            Scopes = ["openid", "profile"],
        };

        var jwt = await builder.BuildAccessTokenAsync(context, ct);

        Assert.NotEmpty(jwt);
        var parts = jwt.Split('.');
        Assert.Equal(3, parts.Length);

        // Decode header
        var header = JsonSerializer.Deserialize<JsonElement>(
            Encoding.UTF8.GetString(Base64UrlDecode(parts[0])));
        Assert.Equal("RS256", header.GetProperty("alg").GetString());
        Assert.Equal("JWT", header.GetProperty("typ").GetString());

        // Decode payload
        var payload = JsonSerializer.Deserialize<JsonElement>(
            Encoding.UTF8.GetString(Base64UrlDecode(parts[1])));
        Assert.Equal("https://localhost:5001", payload.GetProperty("iss").GetString());
        Assert.Equal("user-123", payload.GetProperty("sub").GetString());
        Assert.Equal("test-client", payload.GetProperty("aud").GetString());
        Assert.Equal("openid profile", payload.GetProperty("scope").GetString());
    }

    [Fact]
    public async Task BuildAccessToken_IncludesClientIdClaim_PerRfc9068()
    {
        var ct = TestContext.Current.CancellationToken;
        var builder = new JwtTokenBuilder(new FakeSigningKeyRepository());

        var context = new TokenBuildContext
        {
            Subject = "user-123",
            ClientId = "test-client",
            Issuer = "https://localhost:5001",
            LifetimeSeconds = 3600,
            Scopes = ["openid"],
        };

        var jwt = await builder.BuildAccessTokenAsync(context, ct);
        var payload = JsonSerializer.Deserialize<JsonElement>(
            Encoding.UTF8.GetString(Base64UrlDecode(jwt.Split('.')[1])));

        // RFC 9068 §2.2 — client_id is REQUIRED on JWT access tokens.
        Assert.True(payload.TryGetProperty("client_id", out var clientId));
        Assert.Equal("test-client", clientId.GetString());
    }

    [Fact]
    public async Task BuildIdToken_DoesNotIncludeClientIdClaim()
    {
        var ct = TestContext.Current.CancellationToken;
        var builder = new JwtTokenBuilder(new FakeSigningKeyRepository());

        var context = new TokenBuildContext
        {
            Subject = "user-123",
            ClientId = "test-client",
            Issuer = "https://localhost:5001",
            LifetimeSeconds = 3600,
            Scopes = ["openid"],
        };

        var accessToken = await builder.BuildAccessTokenAsync(context, ct);
        var idToken = await builder.BuildIdTokenAsync(context, accessToken, ct);
        var payload = JsonSerializer.Deserialize<JsonElement>(
            Encoding.UTF8.GetString(Base64UrlDecode(idToken.Split('.')[1])));

        // client_id is an access-token claim (RFC 9068), not an id_token claim (OIDC Core).
        Assert.False(payload.TryGetProperty("client_id", out _));
    }

    [Fact]
    public async Task BuildIdToken_IncludesAtHash()
    {
        var ct = TestContext.Current.CancellationToken;
        var repo = new FakeSigningKeyRepository();
        var builder = new JwtTokenBuilder(repo);

        var context = new TokenBuildContext
        {
            Subject = "user-123",
            ClientId = "test-client",
            Issuer = "https://localhost:5001",
            LifetimeSeconds = 3600,
            Scopes = ["openid"],
            Nonce = "test-nonce",
        };

        var accessToken = await builder.BuildAccessTokenAsync(context, ct);
        var idToken = await builder.BuildIdTokenAsync(context, accessToken, ct);

        var parts = idToken.Split('.');
        var payload = JsonSerializer.Deserialize<JsonElement>(
            Encoding.UTF8.GetString(Base64UrlDecode(parts[1])));

        Assert.True(payload.TryGetProperty("at_hash", out _));
        Assert.Equal("test-nonce", payload.GetProperty("nonce").GetString());
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

    private sealed class FakeSigningKeyRepository : ISigningKeyRepository
    {
        private readonly SigningKey _key;

        public FakeSigningKeyRepository()
        {
            using var rsa = RSA.Create(2048);
            var privateKey = Convert.ToBase64String(rsa.ExportRSAPrivateKey());
            _key = SigningKey.Create("test-key-id", "RS256", "RSA", privateKey);
        }

        public Task<SigningKey?> GetActiveKeyAsync(CancellationToken ct = default) => Task.FromResult<SigningKey?>(_key);
        public Task<SigningKey?> FindByKeyIdAsync(string keyId, CancellationToken ct = default) => Task.FromResult<SigningKey?>(_key);
        public Task<IReadOnlyList<SigningKey>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SigningKey>>([_key]);
        public Task AddAsync(SigningKey key, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(SigningKey key, CancellationToken ct = default) => Task.CompletedTask;
    }
}
