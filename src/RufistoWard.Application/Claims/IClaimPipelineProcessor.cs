namespace RufistoWard.Auth.Claims;

using System.Security.Claims;

public interface IClaimPipelineProcessor
{
    Task<IReadOnlyList<Claim>> ProcessAsync(
        string clientId,
        string subjectId,
        IReadOnlyList<Claim> baseClaims,
        CancellationToken cancellationToken = default);
}
