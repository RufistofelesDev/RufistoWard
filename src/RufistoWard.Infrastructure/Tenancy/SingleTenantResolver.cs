namespace RufistoWard.Tokens.Tenancy;

using RufistoWard.Identity.Interfaces;

/// <summary>
/// Resolves every request to the one tenant a single-tenant deployment has.
/// </summary>
/// <remarks>
/// This is the core's only resolver. It makes the tenant column invisible to deployments that
/// do not need it: every row is stamped with the same value, and every filter compares that
/// value against itself.
/// </remarks>
public sealed class SingleTenantResolver : ITenantResolver
{
    /// <inheritdoc />
    public int Resolve() => TenantDefaults.DefaultTenantId;
}
