namespace RufistoWard.Identity.Entities;

using RufistoWard.Identity.Interfaces;

public sealed class ApiScope : ITenantScoped
{
    public int Id { get; private set; }

    /// <summary>
    /// The tenant this row belongs to. Stamped on insert by the persistence layer.
    /// </summary>
    public int TenantId { get; private set; }
    public string Name { get; private set; } = default!;
    public string? DisplayName { get; private set; }
    public string? Description { get; private set; }
    public bool Enabled { get; private set; } = true;
    public bool Required { get; private set; }
    public bool Emphasize { get; private set; }

    private ApiScope() { }

    public static ApiScope Create(string name, string? displayName = null, string? description = null)
    {
        return new ApiScope { Name = name, DisplayName = displayName, Description = description };
    }

    public void Update(string? displayName, string? description, bool? enabled, bool? required, bool? emphasize)
    {
        if (displayName is not null) DisplayName = displayName;
        if (description is not null) Description = description;
        if (enabled.HasValue) Enabled = enabled.Value;
        if (required.HasValue) Required = required.Value;
        if (emphasize.HasValue) Emphasize = emphasize.Value;
    }
}
