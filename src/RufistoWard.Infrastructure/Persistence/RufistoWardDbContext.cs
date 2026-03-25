namespace RufistoWard.Tokens.Persistence;

using Microsoft.EntityFrameworkCore;
using RufistoWard.Identity.Entities;

public sealed class RufistoWardDbContext(DbContextOptions<RufistoWardDbContext> options)
    : DbContext(options)
{
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
    }
}
