namespace RufistoWard.Identity.Entities;

public sealed class ClientGrantType
{
    public int Id { get; private set; }
    public int ClientId { get; private set; }
    public string GrantType { get; private set; } = default!;

    private ClientGrantType() { }
}
