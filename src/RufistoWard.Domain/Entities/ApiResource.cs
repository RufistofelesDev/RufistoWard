namespace RufistoWard.Identity.Entities;

public sealed class ApiResource
{
    public int Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string? DisplayName { get; private set; }
    public string? Description { get; private set; }
    public bool Enabled { get; private set; } = true;

    private readonly List<ApiResourceScope> _scopes = [];
    public IReadOnlyCollection<ApiResourceScope> Scopes => _scopes.AsReadOnly();

    private ApiResource() { }
}
