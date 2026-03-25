namespace RufistoWard.Identity.Entities;

public sealed class ApiResourceScope
{
    public int Id { get; private set; }
    public int ApiResourceId { get; private set; }
    public string Scope { get; private set; } = default!;

    private ApiResourceScope() { }
}
