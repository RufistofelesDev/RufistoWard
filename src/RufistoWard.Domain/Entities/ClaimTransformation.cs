namespace RufistoWard.Identity.Entities;

public sealed class ClaimTransformation
{
    public int Id { get; private set; }
    public string OperationType { get; private set; } = default!; // Filter, Rename, Enrich
    public string SourceClaim { get; private set; } = default!;
    public string? TargetClaim { get; private set; }
    public string? Value { get; private set; }
    public int SortOrder { get; private set; }

    private ClaimTransformation() { }
}
