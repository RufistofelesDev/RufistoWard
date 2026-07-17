namespace RufistoWard.Identity.Interfaces;

/// <summary>
/// Tenant constants shared by the domain and the persistence layer.
/// </summary>
public static class TenantDefaults
{
    /// <summary>
    /// The tenant every row belongs to in a single-tenant deployment.
    /// </summary>
    /// <remarks>
    /// Deliberately not <c>0</c>: an unstamped row would otherwise be indistinguishable from a
    /// correctly stamped one, and default-valued bugs would read as valid data.
    /// </remarks>
    public const int DefaultTenantId = 1;
}
