namespace RufistoWard.Auth.Tests.Protocol.Token;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RufistoWard.Auth.Protocol.Common;
using RufistoWard.Auth.Protocol.Discovery;
using RufistoWard.Auth.Protocol.Token;
using RufistoWard.Auth.Protocol.Validation;
using RufistoWard.Identity.Entities;
using RufistoWard.Identity.Interfaces;

/// <summary>
/// Handler-level coverage for Story 10.4b. Exercises refresh-token rotation family inheritance and
/// the reuse → whole-family-revocation path (RFC 6749 §10.4 / RFC 6819 §5.2.2.3) using hand-rolled
/// fakes (the project carries no mocking framework).
/// </summary>
[Trait("Category", "Unit")]
public sealed class RefreshTokenHandlerTests
{
    private const string ClientId = "client-1";
    private const string ScopeData = "{\"scope\":\"openid profile\"}";

    [Fact]
    public async Task Refresh_token_reuse_revokes_entire_family()
    {
        var ct = TestContext.Current.CancellationToken;
        var family = Guid.NewGuid();

        var reused = PersistedGrant.Create("reused", "refresh_token", ClientId, "subject-1", ScopeData, familyId: family);
        reused.MarkConsumed(); // already rotated out — presenting it again is reuse
        var sibling = PersistedGrant.Create("sibling", "refresh_token", ClientId, "subject-1", ScopeData,
            expiration: FarFuture, familyId: family);

        var (handler, repo, logger) = CreateHandler(reused, sibling);

        var result = await handler.HandleAsync(Request("reused"), ct);

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_grant", result.Error!.Code);
        Assert.Contains(family, repo.RevokedFamilies);
        Assert.True(sibling.IsConsumed); // the live sibling in the family was revoked
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains(family.ToString()));
    }

    [Fact]
    public async Task Legitimate_rotation_issues_new_grant_with_same_family()
    {
        var ct = TestContext.Current.CancellationToken;
        var family = Guid.NewGuid();
        var active = PersistedGrant.Create("active", "refresh_token", ClientId, "subject-1", ScopeData,
            expiration: FarFuture, familyId: family);

        var (handler, repo, _) = CreateHandler(active);

        var result = await handler.HandleAsync(Request("active"), ct);

        Assert.True(result.IsSuccess);
        var issued = Assert.Single(repo.Added);
        Assert.Equal(family, issued.FamilyId); // descendant inherits the family unchanged
        Assert.Equal(result.Value!.RefreshToken, issued.Id);
        Assert.True(active.IsConsumed); // parent rotated out
        Assert.Empty(repo.RevokedFamilies); // a legit rotation must not trip family revocation
    }

    [Fact]
    public async Task Unknown_refresh_token_is_invalid_grant_without_revocation()
    {
        var ct = TestContext.Current.CancellationToken;
        var (handler, repo, _) = CreateHandler();

        var result = await handler.HandleAsync(Request("nope"), ct);

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_grant", result.Error!.Code);
        Assert.Empty(repo.RevokedFamilies);
    }

    private static DateTime FarFuture => DateTime.UtcNow.AddHours(1);

    private static TokenRequest Request(string refreshToken) => new()
    {
        GrantType = "refresh_token",
        ClientId = ClientId,
        ClientSecret = "secret",
        RefreshToken = refreshToken,
    };

    private static (RefreshTokenHandler handler, FakeGrantRepository repo, CapturingLogger<RefreshTokenHandler> logger)
        CreateHandler(params PersistedGrant[] seed)
    {
        var repo = new FakeGrantRepository(seed);
        var logger = new CapturingLogger<RefreshTokenHandler>();
        var options = Options.Create(new DiscoveryOptions { Issuer = "https://issuer.example" });
        var handler = new RefreshTokenHandler(
            repo,
            new FakeClientAuthenticator(Client.Create(ClientId, "Test Client")),
            new FakeTokenBuilder(),
            options,
            logger);
        return (handler, repo, logger);
    }

    private sealed class FakeGrantRepository : IPersistedGrantRepository
    {
        private readonly Dictionary<string, PersistedGrant> _store = new(StringComparer.Ordinal);
        public List<PersistedGrant> Added { get; } = [];
        public List<Guid> RevokedFamilies { get; } = [];

        public FakeGrantRepository(params PersistedGrant[] seed)
        {
            foreach (var g in seed)
            {
                _store[g.Id] = g;
            }
        }

        public Task<PersistedGrant?> FindByIdAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.GetValueOrDefault(id));

        public Task<IReadOnlyList<PersistedGrant>> FindBySubjectIdAsync(string subjectId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PersistedGrant>>(_store.Values.Where(g => g.SubjectId == subjectId).ToList());

        public Task<IReadOnlyList<PersistedGrant>> FindByTypeAsync(string type, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PersistedGrant>>(_store.Values.Where(g => g.Type == type && !g.IsConsumed).ToList());

        public Task AddAsync(PersistedGrant grant, CancellationToken cancellationToken = default)
        {
            _store[grant.Id] = grant;
            Added.Add(grant);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(PersistedGrant grant, CancellationToken cancellationToken = default)
        {
            _store[grant.Id] = grant;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(PersistedGrant grant, CancellationToken cancellationToken = default)
        {
            _store.Remove(grant.Id);
            return Task.CompletedTask;
        }

        public Task DeleteExpiredAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<int> RevokeFamilyAsync(Guid familyId, CancellationToken cancellationToken = default)
        {
            RevokedFamilies.Add(familyId);
            var count = 0;
            foreach (var g in _store.Values.Where(g => g.FamilyId == familyId && !g.IsConsumed))
            {
                g.MarkConsumed();
                count++;
            }
            return Task.FromResult(count);
        }
    }

    private sealed class FakeClientAuthenticator(Client client) : IClientAuthenticator
    {
        public Task<Result<Client>> AuthenticateAsync(string? clientId, string? clientSecret, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<Client>.Success(client));
    }

    private sealed class FakeTokenBuilder : ITokenBuilder
    {
        public Task<string> BuildAccessTokenAsync(TokenBuildContext context, CancellationToken cancellationToken = default)
            => Task.FromResult("access-token");

        public Task<string> BuildIdTokenAsync(TokenBuildContext context, string accessToken, CancellationToken cancellationToken = default)
            => Task.FromResult("id-token");
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }
}
