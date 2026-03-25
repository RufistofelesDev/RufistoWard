namespace RufistoWard.Identity.Entities;

public sealed class Role
{
    public int Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }

    private readonly List<RolePermission> _permissions = [];
    public IReadOnlyCollection<RolePermission> Permissions => _permissions.AsReadOnly();

    private Role() { }
}
