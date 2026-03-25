namespace RufistoWard.Identity.Entities;

public sealed class ExternalClaimSource
{
    public int Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string Url { get; private set; } = default!;
    public int TimeoutSeconds { get; private set; } = 5;
    public int CacheDurationSeconds { get; private set; } = 300;
    public string FailurePolicy { get; private set; } = "SkipClaims"; // UseCached, FailRequest, SkipClaims
    public string? CustomHeaders { get; private set; } // JSON
    public bool Enabled { get; private set; } = true;
    public int SortOrder { get; private set; }

    private ExternalClaimSource() { }
}
