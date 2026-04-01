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

    public static ApiResource Create(string name, string? displayName = null, string? description = null)
    {
        return new ApiResource { Name = name, DisplayName = displayName, Description = description };
    }

    public void Update(string? displayName, string? description, bool? enabled)
    {
        if (displayName is not null) DisplayName = displayName;
        if (description is not null) Description = description;
        if (enabled.HasValue) Enabled = enabled.Value;
    }

    public void AddScope(string scope)
    {
        if (_scopes.All(s => s.Scope != scope))
            _scopes.Add(new ApiResourceScope(scope));
    }

    public void RemoveScope(string scope)
    {
        var existing = _scopes.FirstOrDefault(s => s.Scope == scope);
        if (existing is not null) _scopes.Remove(existing);
    }
}
