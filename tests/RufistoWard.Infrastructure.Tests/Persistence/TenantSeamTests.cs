namespace RufistoWard.Tokens.Tests.Persistence;

using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RufistoWard.Identity.Entities;
using RufistoWard.Identity.Interfaces;
using RufistoWard.Tokens.Persistence;
using RufistoWard.Tokens.Tenancy;

/// <summary>
/// Tenant isolation tests against a real relational provider (SQLite in-memory).
/// </summary>
/// <remarks>
/// These run against a real provider rather than the in-memory store because the properties under
/// test — composite unique indexes and translated query filters — do not exist in the in-memory
/// provider, so it would pass every one of them without enforcing anything.
/// </remarks>
public sealed class TenantSeamTests : IDisposable
{
    private const int TenantA = 1;
    private const int TenantB = 2;

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<RufistoWardDbContext> _options;

    public TenantSeamTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<RufistoWardDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = NewContext(TenantA);
        context.Database.EnsureCreated();
    }

    private sealed class FixedTenantContext(int tenantId) : ITenantContext
    {
        public int TenantId { get; } = tenantId;
    }

    private RufistoWardDbContext NewContext(int tenantId) =>
        new(_options, new FixedTenantContext(tenantId));

    public void Dispose() => _connection.Dispose();

    [Fact]
    public void SingleTenantResolver_ResolvesToTheDefaultTenant()
    {
        Assert.Equal(TenantDefaults.DefaultTenantId, new SingleTenantResolver().Resolve());
    }

    [Fact]
    public void DefaultTenantId_IsNotZero()
    {
        // A zero default would make an unstamped row indistinguishable from a stamped one, and
        // the AddTenantIdToAggregateRoots backfill depends on this being 1.
        Assert.NotEqual(0, TenantDefaults.DefaultTenantId);
    }

    [Fact]
    public void ResolvedTenantContext_LatchesTheTenantAfterFirstRead()
    {
        var resolver = new CountingResolver();
        var context = new ResolvedTenantContext(resolver);

        _ = context.TenantId;
        _ = context.TenantId;
        _ = context.TenantId;

        // The tenant must not be able to shift midway through a unit of work: a request that read
        // one tenant's rows and wrote another's would be a cross-tenant write that both the filter
        // and the stamp would consider correct.
        Assert.Equal(1, resolver.Calls);
    }

    private sealed class CountingResolver : ITenantResolver
    {
        public int Calls { get; private set; }

        public int Resolve()
        {
            Calls++;
            return TenantDefaults.DefaultTenantId;
        }
    }

    [Fact]
    public async Task Insert_StampsTheCurrentTenant_WithoutTheCallerSayingSo()
    {
        await using (var write = NewContext(TenantB))
        {
            write.Clients.Add(Client.Create("app", "App"));
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = NewContext(TenantB);
        var client = await read.Clients.SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(TenantB, client.TenantId);
    }

    [Fact]
    public async Task Read_DoesNotSeeAnotherTenantsRows()
    {
        await using (var write = NewContext(TenantA))
        {
            write.Clients.Add(Client.Create("app", "App"));
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var other = NewContext(TenantB);

        Assert.Empty(await other.Clients.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Null(await other.Clients.FirstOrDefaultAsync(
            c => c.ClientId == "app", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Read_StillSeesItsOwnRows()
    {
        await using (var write = NewContext(TenantA))
        {
            write.Clients.Add(Client.Create("app", "App"));
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var same = NewContext(TenantA);

        Assert.Single(await same.Clients.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TwoTenantsMayShareAClientId()
    {
        await using (var a = NewContext(TenantA))
        {
            a.Clients.Add(Client.Create("webapp", "Web App"));
            await a.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var b = NewContext(TenantB);
        b.Clients.Add(Client.Create("webapp", "Web App"));

        // The pre-seam index was globally unique on ClientId, so this insert threw. This is the
        // single reason the seam has to ship before the first public release.
        await b.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Single(await b.Clients.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheSameHumanMayExistInTwoTenants()
    {
        await using (var a = NewContext(TenantA))
        {
            a.Users.Add(User.Create("sub-a", "rafael", "rafael@example.com", "hash"));
            await a.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var b = NewContext(TenantB);
        b.Users.Add(User.Create("sub-b", "rafael", "rafael@example.com", "hash"));

        // A contractor working for two customers is one email address in two tenants.
        await b.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Single(await b.Users.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AClientIdStillCannotRepeatWithinOneTenant()
    {
        await using var a = NewContext(TenantA);
        a.Clients.Add(Client.Create("webapp", "Web App"));
        await a.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var again = NewContext(TenantA);
        again.Clients.Add(Client.Create("webapp", "Duplicate"));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => again.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ATrackedRowCannotBeMovedToAnotherTenant()
    {
        await using (var write = NewContext(TenantA))
        {
            write.Clients.Add(Client.Create("app", "App"));
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var move = NewContext(TenantA);
        var client = await move.Clients.SingleAsync(TestContext.Current.CancellationToken);
        move.Entry(client).Property(nameof(ITenantScoped.TenantId)).CurrentValue = TenantB;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => move.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Contains("cannot be written to another tenant", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ADetachedRowCarryingAForeignTenantCannotBeWritten()
    {
        await using (var write = NewContext(TenantB))
        {
            write.Clients.Add(Client.Create("app", "App"));
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Client detached;
        await using (var read = NewContext(TenantB))
        {
            detached = await read.Clients.SingleAsync(TestContext.Current.CancellationToken);
        }

        // Repositories update detached entities, and EF sets OriginalValue equal to CurrentValue
        // for those — so an original-vs-current check would wave this straight through, and the
        // query filter never sees an UPDATE's WHERE clause.
        await using var wrongTenant = NewContext(TenantA);
        wrongTenant.Clients.Update(detached);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => wrongTenant.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Contains("cannot be written to another tenant", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdatingADetachedRowInItsOwnTenantIsAllowed()
    {
        await using (var write = NewContext(TenantA))
        {
            write.Clients.Add(Client.Create("app", "App"));
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Client detached;
        await using (var read = NewContext(TenantA))
        {
            detached = await read.Clients.SingleAsync(TestContext.Current.CancellationToken);
        }

        // The guard must not fire on the ordinary repository path: Update() marks every property
        // modified, TenantId included, even though nothing moved.
        await using var same = NewContext(TenantA);
        detached.Update("Renamed", null, null, null, null);
        same.Clients.Update(detached);

        await same.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var verify = NewContext(TenantA);
        var reloaded = await verify.Clients.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Renamed", reloaded.ClientName);
    }

    [Fact]
    public async Task SigningKeysAreTenantScoped()
    {
        await using (var a = NewContext(TenantA))
        {
            a.SigningKeys.Add(SigningKey.Create("kid-1", "RS256", "RSA", "key-material"));
            await a.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var b = NewContext(TenantB);

        // Tenants that share a signing key are separated only by the iss claim — a string
        // comparison in application code — so a token minted for one stays cryptographically
        // valid for every other. The keys table is the one that must never be migrated wrong,
        // which is why it carries the column from the first release.
        Assert.Empty(await b.SigningKeys.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void EveryTenantScopedRootHasAQueryFilter()
    {
        using var context = NewContext(TenantA);
        var model = context.Model;

        var scopedRoots = model.GetEntityTypes()
            .Where(e => e.ClrType.IsAssignableTo(typeof(ITenantScoped)))
            .ToList();

        Assert.NotEmpty(scopedRoots);

        // A root marked ITenantScoped but never given a filter does not fail — it leaks. This
        // reads the built model rather than the source so that adding a root and forgetting the
        // filter is caught here rather than in production.
        var unfiltered = scopedRoots
            .Where(e => e.GetDeclaredQueryFilters() is not { Count: > 0 })
            .Select(e => e.ClrType.Name)
            .ToList();

        Assert.Empty(unfiltered);
    }

    [Fact]
    public void NoUniqueIndexOmitsTheTenant()
    {
        using var context = NewContext(TenantA);

        // A unique index that omits TenantId silently makes a value global across tenants: the
        // second tenant to use a name simply cannot be created.
        var offenders = context.Model.GetEntityTypes()
            .Where(e => e.ClrType.IsAssignableTo(typeof(ITenantScoped)))
            .SelectMany(e => e.GetIndexes())
            .Where(i => i.IsUnique)
            .Where(i => !i.Properties.Any(p => p.Name == nameof(ITenantScoped.TenantId)))
            .Select(i => $"{i.DeclaringEntityType.ClrType.Name}: {i.GetDatabaseName()}")
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void EveryAggregateRootWithAUniqueIndexIsTenantScoped()
    {
        // Catches the reverse mistake: a new root ships with a unique index and no marker, so
        // nothing above flags it because it was never in scope to begin with.
        var rootsWithUniqueIndexes = typeof(Client).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Where(t => t.Namespace == typeof(Client).Namespace)
            .ToList();

        using var context = NewContext(TenantA);

        var unmarked = context.Model.GetEntityTypes()
            .Where(e => e.GetIndexes().Any(i => i.IsUnique))
            .Where(e => !e.ClrType.IsAssignableTo(typeof(ITenantScoped)))
            .Select(e => e.ClrType.Name)
            .ToList();

        Assert.Empty(unmarked);
    }
}
