namespace RufistoWard.Tokens.Tenancy;

using RufistoWard.Identity.Interfaces;

/// <summary>
/// The tenant for the current scope, resolved once on first read and then fixed.
/// </summary>
/// <remarks>
/// The tenant is latched rather than re-resolved per call so that it cannot change midway
/// through a unit of work. A request that read one tenant's rows and then wrote another's would
/// be a cross-tenant write that no filter or stamp could catch — both would be behaving exactly
/// as told.
/// </remarks>
/// <param name="resolver">Decides which tenant this scope belongs to.</param>
public sealed class ResolvedTenantContext(ITenantResolver resolver) : ITenantContext
{
    private readonly ITenantResolver _resolver = resolver;
    private int? _tenantId;

    /// <inheritdoc />
    public int TenantId => _tenantId ??= _resolver.Resolve();
}
