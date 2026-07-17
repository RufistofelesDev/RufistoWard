namespace RufistoWard.Identity.Interfaces;

/// <summary>
/// Decides which tenant a request belongs to.
/// </summary>
/// <remarks>
/// The core ships a single-tenant implementation that always answers
/// <see cref="TenantDefaults.DefaultTenantId"/>. This interface is the extension point for
/// deployments that serve more than one tenant.
/// <para>
/// A resolver alone does not make a deployment safely multi-tenant. Tenants that share signing
/// keys are separated only by the <c>iss</c> claim — a string comparison in application code —
/// so a token minted for one tenant remains cryptographically valid for every other. Real
/// isolation additionally requires per-tenant signing keys, per-tenant JWKS and discovery, and
/// evidence that a token issued to one tenant is rejected by the next. Implementing this
/// interface without those is isolation in name only.
/// </para>
/// </remarks>
public interface ITenantResolver
{
    /// <summary>
    /// Resolves the tenant for the current request.
    /// </summary>
    /// <returns>The resolved tenant identifier.</returns>
    int Resolve();
}
