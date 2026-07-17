namespace RufistoWard.Identity.Interfaces;

/// <summary>
/// The tenant the current request is operating on.
/// </summary>
/// <remarks>
/// Resolved once per request and read by the persistence layer to stamp inserts and filter
/// reads. Consumers must never take a tenant from user input; that is what the resolver is for.
/// </remarks>
public interface ITenantContext
{
    /// <summary>
    /// The current tenant. Always <see cref="TenantDefaults.DefaultTenantId"/> in a
    /// single-tenant deployment.
    /// </summary>
    int TenantId { get; }
}
