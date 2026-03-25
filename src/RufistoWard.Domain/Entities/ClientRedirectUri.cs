namespace RufistoWard.Identity.Entities;

public sealed class ClientRedirectUri
{
    public int Id { get; private set; }
    public int ClientId { get; private set; }
    public string Uri { get; private set; } = default!;

    private ClientRedirectUri() { }
}
