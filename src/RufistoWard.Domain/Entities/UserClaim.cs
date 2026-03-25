namespace RufistoWard.Identity.Entities;

public sealed class UserClaim
{
    public int Id { get; private set; }
    public int UserId { get; private set; }
    public string Type { get; private set; } = default!;
    public string Value { get; private set; } = default!;

    private UserClaim() { }
}
