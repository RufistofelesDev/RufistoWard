namespace RufistoWard.Identity.Entities;

public sealed class Role
{
    public int Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }

    private readonly List<RolePermission> _permissions = [];
    public IReadOnlyCollection<RolePermission> Permissions => _permissions.AsReadOnly();

    private Role() { }

    public static Role Create(string name, string? description = null)
    {
        return new Role { Name = name, Description = description };
    }

    public void Update(string? name, string? description)
    {
        if (name is not null) Name = name;
        if (description is not null) Description = description;
    }

    public void AddPermission(string permission)
    {
        if (_permissions.All(p => p.Permission != permission))
            _permissions.Add(new RolePermission(permission));
    }

    public void RemovePermission(string permission)
    {
        var existing = _permissions.FirstOrDefault(p => p.Permission == permission);
        if (existing is not null) _permissions.Remove(existing);
    }
}
