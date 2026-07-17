namespace RufistoWard.Tokens.Persistence;

using Microsoft.EntityFrameworkCore;
using RufistoWard.Identity.Entities;
using RufistoWard.Identity.Interfaces;

public sealed class RufistoWardDbContext(
    DbContextOptions<RufistoWardDbContext> options,
    ITenantContext tenantContext)
    : DbContext(options)
{
    private readonly ITenantContext _tenantContext = tenantContext;

    public DbSet<Client> Clients => Set<Client>();
    public DbSet<ClientSecret> ClientSecrets => Set<ClientSecret>();
    public DbSet<ClientScope> ClientScopes => Set<ClientScope>();
    public DbSet<ClientRedirectUri> ClientRedirectUris => Set<ClientRedirectUri>();
    public DbSet<ClientGrantType> ClientGrantTypes => Set<ClientGrantType>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserClaim> UserClaims => Set<UserClaim>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<ApiScope> ApiScopes => Set<ApiScope>();
    public DbSet<ApiResource> ApiResources => Set<ApiResource>();
    public DbSet<ApiResourceScope> ApiResourceScopes => Set<ApiResourceScope>();
    public DbSet<PersistedGrant> PersistedGrants => Set<PersistedGrant>();
    public DbSet<DeviceCode> DeviceCodes => Set<DeviceCode>();
    public DbSet<SigningKey> SigningKeys => Set<SigningKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RufistoWardDbContext).Assembly);

        // Every root marked ITenantScoped is filtered to the current tenant. The filters live
        // here rather than in each IEntityTypeConfiguration because a missing one does not fail
        // — it leaks — so they are kept in one list that can be read against the model in a test.
        modelBuilder.Entity<Client>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<User>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<Role>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<ApiScope>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<ApiResource>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<PersistedGrant>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<DeviceCode>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<SigningKey>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampTenantOnNewRows();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        StampTenantOnNewRows();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Assigns the current tenant to every inserted tenant-scoped row, and refuses to let an
    /// existing row change tenant.
    /// </summary>
    /// <remarks>
    /// Stamping happens here rather than in the entity factories so that no call site can create
    /// a row in the wrong tenant, or forget to say which — callers never supply a tenant at all,
    /// so there is no argument for them to get wrong.
    /// </remarks>
    private void StampTenantOnNewRows()
    {
        foreach (var entry in ChangeTracker.Entries<ITenantScoped>())
        {
            var tenant = entry.Property(nameof(ITenantScoped.TenantId));

            switch (entry.State)
            {
                case EntityState.Added:
                    tenant.CurrentValue = _tenantContext.TenantId;
                    break;

                // A write must land in the tenant that is in context, whatever route the entity
                // took to get here. Comparing the original value against the current one would
                // not do: repositories update detached entities, and EF sets OriginalValue equal
                // to CurrentValue for those, so a fabricated row carrying a foreign tenant would
                // compare clean and be written across the boundary. The query filter does not
                // cover this — it constrains reads, not the WHERE clause of an UPDATE.
                case EntityState.Modified when !Equals(tenant.CurrentValue, _tenantContext.TenantId):
                    throw new InvalidOperationException(
                        $"{entry.Entity.GetType().Name} cannot be written to another tenant " +
                        $"(row is tenant {tenant.CurrentValue}, context is tenant {_tenantContext.TenantId}).");

                default:
                    break;
            }
        }
    }
}
