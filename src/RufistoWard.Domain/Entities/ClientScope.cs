namespace RufistoWard.Identity.Entities;

public sealed class ClientScope
{
    public int Id { get; private set; }
    public int ClientId { get; private set; }
    public string Scope { get; private set; } = default!;

    private ClientScope() { }
}
