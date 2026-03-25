namespace RufistoWard.Identity.Entities;

public sealed class ClaimMapping
{
    public int Id { get; private set; }
    public string SourceClaim { get; private set; } = default!;
    public string TargetClaim { get; private set; } = default!;
    public string InclusionTarget { get; private set; } = "Both"; // Identity, Access, Both
    public int SortOrder { get; private set; }
    public string? ClientId { get; private set; } // null = all clients

    private ClaimMapping() { }
}
