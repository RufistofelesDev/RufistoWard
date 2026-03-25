namespace RufistoWard.Auth.Claims;

using System.Security.Claims;

public sealed class ClaimPipelineProcessor : IClaimPipelineProcessor
{
    // MVP: pass-through processor — full pipeline implementation in growth
    public Task<IReadOnlyList<Claim>> ProcessAsync(
        string clientId,
        string subjectId,
        IReadOnlyList<Claim> baseClaims,
        CancellationToken cancellationToken = default)
    {
        // Stage 1: Static mappings — pass through base claims for now
        // Stage 2: Transformations — no-op for now
        // Stage 3: External sources — no-op for now
        return Task.FromResult(baseClaims);
    }
}
