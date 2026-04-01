namespace RufistoWard.Identity.Entities;

public sealed class RolePermission
{
    public int Id { get; private set; }
    public int RoleId { get; private set; }
    public string Permission { get; private set; } = default!;

    private RolePermission() { }

    internal RolePermission(string permission)
    {
        Permission = permission;
    }
}
