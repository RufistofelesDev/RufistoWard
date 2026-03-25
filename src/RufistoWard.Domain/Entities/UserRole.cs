namespace RufistoWard.Identity.Entities;

public sealed class UserRole
{
    public int UserId { get; private set; }
    public int RoleId { get; private set; }

    private UserRole() { }
}
