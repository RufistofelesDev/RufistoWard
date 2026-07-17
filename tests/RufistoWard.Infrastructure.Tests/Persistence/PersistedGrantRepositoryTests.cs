namespace RufistoWard.Tokens.Tests.Persistence;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RufistoWard.Identity.Entities;
using RufistoWard.Tokens.Persistence;
using RufistoWard.Tokens.Persistence.Repositories;
using RufistoWard.Tokens.Tenancy;

/// <summary>
/// Direct persistence tests for <see cref="PersistedGrantRepository"/> against a real relational
/// provider (SQLite in-memory). Each test runs its write and read on separate DbContext instances
/// sharing one open connection, so assertions exercise the actual round-trip through SQL rather than
/// the change tracker. The connection is kept open for the fixture lifetime — closing it drops the DB.
/// Schema is materialised with EnsureCreated because the committed migrations are SQL-Server-shaped.
/// Chain-revocation paths (FamilyId / RevokeFamilyAsync) are out of scope — deferred to Story 10.4b.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PersistedGrantRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<RufistoWardDbContext> _options;

    public PersistedGrantRepositoryTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<RufistoWardDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = NewContext();
        context.Database.EnsureCreated();
    }

    private RufistoWardDbContext NewContext() =>
        new(_options, new ResolvedTenantContext(new SingleTenantResolver()));

    private static PersistedGrant NewGrant(
        string id,
        string type = "refresh_token",
        string clientId = "client-1",
        string? subjectId = "subject-1",
        string data = "payload",
        DateTime? expiration = null,
        Guid? familyIdOverride = null)
        => PersistedGrant.Create(id, type, clientId, subjectId, data, expiration, familyIdOverride);

    [Fact]
    public async Task AddAsync_ThenFindById_RoundTripsByHandle()
    {
        var ct = TestContext.Current.CancellationToken;
        await new PersistedGrantRepository(NewContext()).AddAsync(NewGrant("handle-1", data: "the-data"), ct);

        var loaded = await new PersistedGrantRepository(NewContext()).FindByIdAsync("handle-1", ct);

        Assert.NotNull(loaded);
        Assert.Equal("handle-1", loaded.Id);
        Assert.Equal("the-data", loaded.Data);
        Assert.Equal("client-1", loaded.ClientId);
    }

    [Fact]
    public async Task FindByIdAsync_UnknownHandle_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;

        var loaded = await new PersistedGrantRepository(NewContext()).FindByIdAsync("does-not-exist", ct);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task FindBySubjectIdAsync_ReturnsOnlyGrantsForThatSubject()
    {
        var ct = TestContext.Current.CancellationToken;
        var writer = new PersistedGrantRepository(NewContext());
        await writer.AddAsync(NewGrant("a", subjectId: "alice"), ct);
        await writer.AddAsync(NewGrant("b", subjectId: "alice"), ct);
        await writer.AddAsync(NewGrant("c", subjectId: "bob"), ct);

        var alice = await new PersistedGrantRepository(NewContext()).FindBySubjectIdAsync("alice", ct);

        Assert.Equal(2, alice.Count);
        Assert.All(alice, g => Assert.Equal("alice", g.SubjectId));
    }

    [Fact]
    public async Task FindByTypeAsync_ReturnsOnlyUnconsumedGrantsOfThatType()
    {
        var ct = TestContext.Current.CancellationToken;
        var writer = new PersistedGrantRepository(NewContext());
        await writer.AddAsync(NewGrant("rt-1", type: "refresh_token"), ct);
        await writer.AddAsync(NewGrant("rt-2", type: "refresh_token"), ct);
        await writer.AddAsync(NewGrant("ac-1", type: "authorization_code"), ct);

        var refreshTokens = await new PersistedGrantRepository(NewContext()).FindByTypeAsync("refresh_token", ct);

        Assert.Equal(2, refreshTokens.Count);
        Assert.All(refreshTokens, g => Assert.Equal("refresh_token", g.Type));
    }

    [Fact]
    public async Task FindByTypeAsync_ExcludesConsumedGrants()
    {
        var ct = TestContext.Current.CancellationToken;
        await new PersistedGrantRepository(NewContext()).AddAsync(NewGrant("rt-live", type: "refresh_token"), ct);

        var toConsume = NewGrant("rt-consumed", type: "refresh_token");
        toConsume.MarkConsumed();
        await new PersistedGrantRepository(NewContext()).AddAsync(toConsume, ct);

        var live = await new PersistedGrantRepository(NewContext()).FindByTypeAsync("refresh_token", ct);

        Assert.Single(live);
        Assert.Equal("rt-live", live[0].Id);
    }

    [Fact]
    public async Task UpdateAsync_PersistsDataChange()
    {
        var ct = TestContext.Current.CancellationToken;
        await new PersistedGrantRepository(NewContext()).AddAsync(NewGrant("upd-1", data: "before"), ct);

        var loaded = await new PersistedGrantRepository(NewContext()).FindByIdAsync("upd-1", ct);
        loaded!.UpdateData("after");
        await new PersistedGrantRepository(NewContext()).UpdateAsync(loaded, ct);

        var reloaded = await new PersistedGrantRepository(NewContext()).FindByIdAsync("upd-1", ct);
        Assert.Equal("after", reloaded!.Data);
    }

    [Fact]
    public async Task RevokingSameGrantTwice_IsIdempotent_AndKeepsConsumedAtStable()
    {
        var ct = TestContext.Current.CancellationToken;
        await new PersistedGrantRepository(NewContext()).AddAsync(NewGrant("rev-1", type: "refresh_token"), ct);

        // First revoke: mark consumed and persist.
        var first = await new PersistedGrantRepository(NewContext()).FindByIdAsync("rev-1", ct);
        if (!first!.IsConsumed)
        {
            first.MarkConsumed();
        }
        await new PersistedGrantRepository(NewContext()).UpdateAsync(first, ct);
        var consumedAtAfterFirst = first.ConsumedAt;

        // Second revoke of the same handle — must succeed without error and not re-stamp.
        var second = await new PersistedGrantRepository(NewContext()).FindByIdAsync("rev-1", ct);
        if (!second!.IsConsumed)
        {
            second.MarkConsumed();
        }
        await new PersistedGrantRepository(NewContext()).UpdateAsync(second, ct);

        var reloaded = await new PersistedGrantRepository(NewContext()).FindByIdAsync("rev-1", ct);
        Assert.True(reloaded!.IsConsumed);
        Assert.Equal(consumedAtAfterFirst, reloaded.ConsumedAt);
    }

    [Fact]
    public async Task DeleteAsync_RemovesGrant()
    {
        var ct = TestContext.Current.CancellationToken;
        await new PersistedGrantRepository(NewContext()).AddAsync(NewGrant("del-1"), ct);

        var loaded = await new PersistedGrantRepository(NewContext()).FindByIdAsync("del-1", ct);
        await new PersistedGrantRepository(NewContext()).DeleteAsync(loaded!, ct);

        Assert.Null(await new PersistedGrantRepository(NewContext()).FindByIdAsync("del-1", ct));
    }

    [Fact]
    public async Task DeleteExpiredAsync_RemovesExpired_KeepsActiveAndNeverExpiring()
    {
        var ct = TestContext.Current.CancellationToken;
        var now = DateTime.UtcNow;
        var writer = new PersistedGrantRepository(NewContext());
        await writer.AddAsync(NewGrant("expired", expiration: now.AddMinutes(-5)), ct);
        await writer.AddAsync(NewGrant("active", expiration: now.AddMinutes(5)), ct);
        await writer.AddAsync(NewGrant("eternal", expiration: null), ct);

        await new PersistedGrantRepository(NewContext()).DeleteExpiredAsync(ct);

        var reader = new PersistedGrantRepository(NewContext());
        Assert.Null(await reader.FindByIdAsync("expired", ct));
        Assert.NotNull(await reader.FindByIdAsync("active", ct));
        Assert.NotNull(await reader.FindByIdAsync("eternal", ct));
    }

    [Fact]
    public async Task AddAsync_ThenFindById_RoundTripsFamilyId()
    {
        var ct = TestContext.Current.CancellationToken;
        var familyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        await new PersistedGrantRepository(NewContext())
            .AddAsync(PersistedGrant.Create("fam-rt", "refresh_token", "client-1", "subject-1", "payload", familyId: familyId), ct);

        var loaded = await new PersistedGrantRepository(NewContext()).FindByIdAsync("fam-rt", ct);

        Assert.NotNull(loaded);
        Assert.Equal(familyId, loaded.FamilyId);
    }

    [Fact]
    public async Task Create_WithoutFamilyId_StampsDistinctFamilies()
    {
        // Each root grant must get its own family so reuse of one cannot revoke an unrelated chain.
        var a = PersistedGrant.Create("a", "refresh_token", "c", "s", "d");
        var b = PersistedGrant.Create("b", "refresh_token", "c", "s", "d");

        Assert.NotEqual(Guid.Empty, a.FamilyId);
        Assert.NotEqual(a.FamilyId, b.FamilyId);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task RevokeFamilyAsync_RevokesEntireFamily_ReturnsCount()
    {
        // Fulfils the Story 10.4b tripwire at the persistence layer: revoking by family consumes
        // every unconsumed grant sharing the FamilyId in a single operation.
        var ct = TestContext.Current.CancellationToken;
        var family = Guid.NewGuid();
        var writer = new PersistedGrantRepository(NewContext());
        await writer.AddAsync(NewGrant("f1", familyIdOverride: family), ct);
        await writer.AddAsync(NewGrant("f2", familyIdOverride: family), ct);
        await writer.AddAsync(NewGrant("f3", familyIdOverride: family), ct);

        var revoked = await new PersistedGrantRepository(NewContext()).RevokeFamilyAsync(family, ct);

        Assert.Equal(3, revoked);
        var reader = new PersistedGrantRepository(NewContext());
        foreach (var id in new[] { "f1", "f2", "f3" })
        {
            var g = await reader.FindByIdAsync(id, ct);
            Assert.True(g!.IsConsumed);
        }
    }

    [Fact]
    public async Task RevokeFamilyAsync_LeavesOtherFamiliesUntouched()
    {
        var ct = TestContext.Current.CancellationToken;
        var target = Guid.NewGuid();
        var other = Guid.NewGuid();
        var writer = new PersistedGrantRepository(NewContext());
        await writer.AddAsync(NewGrant("t1", familyIdOverride: target), ct);
        await writer.AddAsync(NewGrant("o1", familyIdOverride: other), ct);

        await new PersistedGrantRepository(NewContext()).RevokeFamilyAsync(target, ct);

        var reader = new PersistedGrantRepository(NewContext());
        Assert.True((await reader.FindByIdAsync("t1", ct))!.IsConsumed);
        Assert.False((await reader.FindByIdAsync("o1", ct))!.IsConsumed);
    }

    [Fact]
    public async Task RevokeFamilyAsync_SecondCall_IsNoOp_ReturnsZero()
    {
        // Idempotent / concurrent-reuse safe: only unconsumed grants are counted, so a repeat
        // revoke of the same family revokes nothing.
        var ct = TestContext.Current.CancellationToken;
        var family = Guid.NewGuid();
        await new PersistedGrantRepository(NewContext()).AddAsync(NewGrant("c1", familyIdOverride: family), ct);

        var first = await new PersistedGrantRepository(NewContext()).RevokeFamilyAsync(family, ct);
        var second = await new PersistedGrantRepository(NewContext()).RevokeFamilyAsync(family, ct);

        Assert.Equal(1, first);
        Assert.Equal(0, second);
    }

    [Fact]
    public async Task DeleteExpiredAsync_WithNothingExpired_RemovesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var now = DateTime.UtcNow;
        var writer = new PersistedGrantRepository(NewContext());
        await writer.AddAsync(NewGrant("a", expiration: now.AddHours(1)), ct);
        await writer.AddAsync(NewGrant("b", expiration: null), ct);

        await new PersistedGrantRepository(NewContext()).DeleteExpiredAsync(ct);

        var reader = new PersistedGrantRepository(NewContext());
        Assert.NotNull(await reader.FindByIdAsync("a", ct));
        Assert.NotNull(await reader.FindByIdAsync("b", ct));
    }

    public void Dispose() => _connection.Dispose();
}
