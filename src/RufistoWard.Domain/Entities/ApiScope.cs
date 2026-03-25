namespace RufistoWard.Identity.Entities;

public sealed class ApiScope
{
    public int Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string? DisplayName { get; private set; }
    public string? Description { get; private set; }
    public bool Enabled { get; private set; } = true;
    public bool Required { get; private set; }
    public bool Emphasize { get; private set; }

    private ApiScope() { }
}
