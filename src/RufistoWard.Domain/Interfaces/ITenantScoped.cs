namespace RufistoWard.Identity.Interfaces;

/// <summary>
/// Marks an aggregate root whose rows belong to exactly one tenant.
/// </summary>
/// <remarks>
/// Implementing this is what subjects an entity to tenant stamping on insert and to the
/// global query filter on read. Child entities are deliberately not tenant-scoped: they are
/// only ever reached through their root and are cascade-deleted with it, so a tenant column
/// on them would be a second source of truth that can disagree with the first.
/// <para>
/// In a single-tenant deployment every row carries <see cref="TenantDefaults.DefaultTenantId"/>
/// and the filter is a constant-true comparison. The column exists so that the unique indexes
/// can be tenant-composite from the first public release — recomposing them later would be a
/// coordinated schema migration against live identity servers.
/// </para>
/// </remarks>
public interface ITenantScoped
{
    /// <summary>
    /// The tenant this row belongs to. Stamped on insert; never assigned by callers.
    /// </summary>
    int TenantId { get; }
}
